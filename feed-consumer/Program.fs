﻿module FeedConsumerTemplate.Program

open Equinox.CosmosStore
open Serilog
open System

exception MissingArg of message : string with override this.Message = this.message

type Configuration(tryGet) =

    let get key =
        match tryGet key with
        | Some value -> value
        | None -> raise (MissingArg (sprintf "Missing Argument/Environment Variable %s" key))

    member _.EquinoxCosmosConnection        = get "EQUINOX_COSMOS_CONNECTION"
    member _.EquinoxCosmosDatabase          = get "EQUINOX_COSMOS_DATABASE"
    member _.EquinoxCosmosContainer         = get "EQUINOX_COSMOS_CONTAINER"

    member _.BaseUri                        = get "API_BASE_URI"
    member _.Group                          = get "API_CONSUMER_GROUP"

module Args =

    open Argu
    open Equinox.CosmosStore
    [<NoEquality; NoComparison>]
    type Parameters =
        | [<AltCommandLine "-V"; Unique>]   Verbose

        | [<AltCommandLine "-g"; Unique>]   Group of string
        | [<AltCommandLine "-f"; Unique>]   BaseUri of string

        | [<AltCommandLine "-r"; Unique>]   MaxReadAhead of int
        | [<AltCommandLine "-w"; Unique>]   FcsDop of int
        | [<AltCommandLine "-t"; Unique>]   TicketsDop of int

        | [<CliPrefix(CliPrefix.None); Unique; Last>] Cosmos of ParseResults<CosmosParameters>
        interface IArgParserTemplate with
            member a.Usage = a |> function
                | Verbose _ ->              "request verbose logging."
                | Group _ ->                "specify Api Consumer Group Id. (optional if environment variable API_CONSUMER_GROUP specified)"
                | BaseUri _ ->              "specify Api endpoint. (optional if environment variable API_BASE_URI specified)"
                | MaxReadAhead _ ->         "maximum number of batches to let processing get ahead of completion. Default: 8."
                | FcsDop _ ->               "maximum number of FCs to process in parallel. Default: 4"
                | TicketsDop _ ->           "maximum number of Tickets to process in parallel (per FC). Default: 4"
                | Cosmos _ ->               "Cosmos Store parameters."
    and Arguments(c : Configuration, a : ParseResults<Parameters>) =
        member val Verbose =                a.Contains Parameters.Verbose
        member val Group =                  a.TryGetResult Group        |> Option.defaultWith (fun () -> c.Group)
        member val BaseUri =                a.TryGetResult BaseUri  |> Option.defaultWith (fun () -> c.BaseUri) |> Uri
        member val MaxReadAhead =           a.GetResult(MaxReadAhead,8)
        member val FcsDop =                 a.TryGetResult FcsDop       |> Option.defaultValue 4
        member val TicketsDop =             a.TryGetResult TicketsDop   |> Option.defaultValue 4
        member val StatsInterval =          TimeSpan.FromMinutes 1.
        member val StateInterval =          TimeSpan.FromMinutes 5.
        member val CheckpointInterval =     TimeSpan.FromHours 1.
        member val Cosmos : CosmosArguments =
            match a.TryGetSubCommand() with
            | Some (Cosmos cosmos) -> CosmosArguments(c, cosmos)
            | _ -> raise (MissingArg "Must specify cosmos")
    and [<NoEquality; NoComparison>] CosmosParameters =
        | [<AltCommandLine "-cm">]          ConnectionMode of Microsoft.Azure.Cosmos.ConnectionMode
        | [<AltCommandLine "-s">]           Connection of string
        | [<AltCommandLine "-d">]           Database of string
        | [<AltCommandLine "-c">]           Container of string
        | [<AltCommandLine "-o">]           Timeout of float
        | [<AltCommandLine "-r">]           Retries of int
        | [<AltCommandLine "-rt">]          RetriesWaitTime of float
        interface IArgParserTemplate with
            member a.Usage = a |> function
                | ConnectionMode _ ->       "override the connection mode. Default: Direct."
                | Connection _ ->           "specify a connection string for a Cosmos account. (optional if environment variable EQUINOX_COSMOS_CONNECTION specified)"
                | Database _ ->             "specify a database name for Cosmos store. (optional if environment variable EQUINOX_COSMOS_DATABASE specified)"
                | Container _ ->            "specify a container name for Cosmos store. (optional if environment variable EQUINOX_COSMOS_CONTAINER specified)"
                | Timeout _ ->              "specify operation timeout in seconds (default: 30)."
                | Retries _ ->              "specify operation retries (default: 9)."
                | RetriesWaitTime _ ->      "specify max wait-time for retry when being throttled by Cosmos in seconds (default: 30)"
    and CosmosArguments(c : Configuration, a : ParseResults<CosmosParameters>) =
        member val Mode =                   a.GetResult(ConnectionMode, Microsoft.Azure.Cosmos.ConnectionMode.Direct)
        member val Connection =             a.TryGetResult Connection |> Option.defaultWith (fun () -> c.EquinoxCosmosConnection) |> Discovery.ConnectionString

        member val Timeout =                a.GetResult(Timeout, 30.) |> TimeSpan.FromSeconds
        member val Retries =                a.GetResult(Retries, 9)
        member val MaxRetryWaitTime =       a.GetResult(RetriesWaitTime, 30.) |> TimeSpan.FromSeconds

        member x.CreateConnector() =
            let discovery = x.Connection
            Log.Information("CosmosDb {mode} {endpointUri} timeout {timeout}s; Throttling retries {retries}, max wait {maxRetryWaitTime}s",
                x.Mode, discovery.Endpoint, (let t = x.Timeout in t.TotalSeconds), x.Retries, (let t = x.MaxRetryWaitTime in t.TotalSeconds))
            CosmosClientFactory(x.Timeout, x.Retries, x.MaxRetryWaitTime, mode=x.Mode)
                .Connect(discovery)

        member val Database =               a.TryGetResult Database   |> Option.defaultWith (fun () -> c.EquinoxCosmosDatabase)
        member val Container =              a.TryGetResult Container  |> Option.defaultWith (fun () -> c.EquinoxCosmosContainer)
        member x.Connect(connector) =
            Log.Information("CosmosDb Database {database} Container {container}", x.Database, x.Container)
            CosmosStoreClient.Connect(connector, x.Database, x.Container)

    /// Parse the commandline; can throw exceptions in response to missing arguments and/or `-h`/`--help` args
    let parse tryGetConfigValue argv =
        let programName = Reflection.Assembly.GetEntryAssembly().GetName().Name
        let parser = ArgumentParser.Create<Parameters>(programName = programName)
        Arguments(Configuration tryGetConfigValue, parser.ParseCommandLine argv)

let [<Literal>] AppName = "FeedConsumerTemplate"

let build (args : Args.Arguments) =
    let target = args.Cosmos
    let connector = target.CreateConnector()
    let client = target.Connect connector |> Async.RunSynchronously
    let context = CosmosStoreContext(client, tipMaxEvents=100)
    let cache = Equinox.Cache (AppName, sizeMb = 10)

    let checkpoints = Propulsion.Feed.ReaderCheckpoint.CosmosStore.create Log.Logger (context, cache)
    let sourceId, tailSleepInterval = Propulsion.Feed.SourceId.parse "FeedConsumerTemplate", TimeSpan.FromSeconds 1.

    let feed = ApiClient.TicketsFeed args.BaseUri

    let handle = Ingester.handle args.TicketsDop
    let stats = Ingester.Stats(Log.Logger, args.StatsInterval, args.StateInterval)
    let sink = Propulsion.Streams.StreamsProjector.Start(Log.Logger, args.MaxReadAhead, args.FcsDop, handle, stats, args.StatsInterval)
    let source =
        Propulsion.Feed.FeedSource(
            Log.Logger, args.StatsInterval, sourceId, tailSleepInterval,
            checkpoints, args.CheckpointInterval, feed.Poll, sink)
    let pumpSource = source.Pump feed.ReadTranches
    sink, pumpSource

let run args = async {
    let sink, pumpSource = build args
    let! _source = pumpSource |> Async.StartChild
    do! sink.AwaitCompletion()
    return if sink.RanToCompletion then 0 else 3
}

[<EntryPoint>]
let main argv =
    try let args = Args.parse EnvVar.tryGet argv
        try Log.Logger <- LoggerConfiguration().Configure(verbose=args.Verbose).CreateLogger()
            try run args |> Async.RunSynchronously
            with e when not (e :? MissingArg) -> Log.Fatal(e, "Exiting"); 2
        finally Log.CloseAndFlush()
    with MissingArg msg -> eprintfn "%s" msg; 1
        | :? Argu.ArguParseException as e -> eprintfn "%s" e.Message; 1
        | e -> eprintf "Exception %s" e.Message; 1
