# Jet `dotnet new` Templates [![Build Status](https://dev.azure.com/jet-opensource/opensource/_apis/build/status/jet.dotnet-templates?branchName=master)](https://dev.azure.com/jet-opensource/opensource/_build/latest?definitionId=9?branchName=master) [![release](https://img.shields.io/github/release/jet/dotnet-templates.svg)](https://github.com/jet/dotnet-templates/releases) [![NuGet](https://img.shields.io/nuget/vpre/Equinox.Templates.svg?logo=nuget)](https://www.nuget.org/packages/Equinox.Templates) [![license](https://img.shields.io/github/license/jet/dotnet-templates.svg)](LICENSE) ![code size](https://img.shields.io/github/languages/code-size/jet/dotnet-templates.svg) [<img src="https://img.shields.io/badge/slack-DDD--CQRS--ES%20%23equinox-yellow.svg?logo=slack">](https://t.co/MRxpx0rLH2)

This repo hosts the source for Jet's [`dotnet new`](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-new) templates.

## [Equinox](https://github.com/jet/equinox) related

- [`eqxweb`](equinox-web/README.md) - Boilerplate for an ASP .NET Core 2 Web App, with an associated storage-independent Domain project using [Equinox](https://github.com/jet/equinox).
- [`eqxwebcs`](equinox-web-csharp/README.md) - Boilerplate for an ASP .NET Core 2 Web App, with an associated storage-independent Domain project using [Equinox](https://github.com/jet/equinox), _ported to C#_.
- [`eqxtestbed`](equinox-testbed/README.md) - Host that allows running back-to-back benchmarks when prototyping models using [Equinox](https://github.com/jet/equinox), using different stores and/or store configuration parameters.

## [Propulsion](https://github.com/jet/propulsion) related

- [`proProjector`](propulsion-projector/README.md) - Boilerplate for an Azure CosmosDb ChangeFeedProcessor (typically unrolling events from `Equinox.Cosmos` stores using `Propulsion.Cosmos`)

  `-k` adds Optional projection to Apache Kafka using [`Propulsion.Kafka`](https://github.com/jet/propulsion).
  
  `-p` shows parallel consumption mode (where source is not stream-oriented; i.e. is not from `Equinox.Cosmos`)

- [`proConsumer`](propulsion-consumer/README.md) - Boilerplate for an Apache Kafka Consumer using [`Propulsion.Kafka`](https://github.com/jet/propulsion). (typically consuming from an app produced with `dotnet new proProjector -k`)

## Producer/Reactor Templates combining usage of Equinox and Propulsion

- [`proReactor`](propulsion-reactor/README.md) - Boilerplate for a dual mode CosmosDB ChangeFeed Processor and/or EventStore `$all` stream projector/reactor using `Propulsion.Cosmos`/`Propulsion.EventStore`

  **NOTE At present, checkpoint storage when projecting from EventStore uses Azure CosmosDB - help wanted ;)**

   Standard processing shows importing (in summary form) from an aggregate in `EventStore` or `Cosmos` to a Summary form in `Cosmos` (use `-b`(`lank`) to remove, yielding a minimal projector)
   
   `--kafka` adds Optional projection to Apache Kafka using [`Propulsion.Kafka`](https://github.com/jet/propulsion) (instead of ingesting into a local `Cosmos` store). Produces versioned [Summary Event](http://verraes.net/2019/05/patterns-for-decoupling-distsys-summary-event/) feed.
   `--raw` (custom option for `--kafka`) emits the raw events encountered on the stream, rather than producing versioned summaries.
   `--noEventStore` removes support for projecting from EventStore from the emitted code
  
- [`proSync`](propulsion-sync/README.md) - Boilerplate for a console app that that syncs events between [`Equinox.Cosmos` and `Equinox.EventStore` stores](https://github.com/jet/equinox) using the [relevant `Propulsion`.* libraries](https://github.com/jet/propulsion), filtering/enriching/mapping Events as necessary.

## Consumer Templates combining usage of Equinox and Propulsion

- [`summaryConsumer`](propulsion-summary-consumer/README.md) - Boilerplate for an Apache Kafka Consumer using [`Propulsion.Kafka`](https://github.com/jet/propulsion) to ingest versioned summaries produced by a `dotnet new proReactor --kafka`

- [`trackingConsumer`](propulsion-tracking-consumer/README.md) - Boilerplate for an Apache Kafka Consumer using [`Propulsion.Kafka`](https://github.com/jet/propulsion) to ingest accumulating changes in an `Equinox.Cosmos` store idempotently.

## Walkthrough

As dictated by [the design of dotnet's templating mechanism](https://github.com/dotnet/templating/), consumption is ultimately via the .NET Core SDK's `dotnet new` CLI facility and/or associated facilities in Visual Studio, Rider etc.

To use from the command line, the outline is:
  1. Install a template locally (use `dotnet new --list` to view your current list)
  2. Use `dotnet new` to expand the template in a given directory

    # install the templates into `dotnet new`s list of available templates so it can be picked up by
    # `dotnet new`, Rider, Visual Studio etc.
    dotnet new -i Equinox.Templates

    # --help shows the options including wiring for storage subsystems,
    # -t includes an example Domain, Handler, Service and Controller to test from app to storage subsystem
    dotnet new eqxweb -t --help

    # if you want to see a C# equivalent:
    dotnet new eqxwebcs -t

    # see readme.md in the generated code for further instructions regarding the TodoBackend the above -t switch above triggers the inclusion of
    start readme.md

    # ... to add a Projector
    md -p ../Projector | Set-Location
    # (-k emits to Kafka and hence implies having a Consumer)
    dotnet new proProjector -k
    start README.md

    # ... to add a Consumer (proProjector -k emits to Kafka and hence implies having a Consumer)
    md -p ../Consumer | Set-Location
    dotnet new proConsumer
    start README.md

    # ... to add a Summary Projector
    md -p ../SummaryProducer | Set-Location
    dotnet new proReactor --kafka --noFilter
    start README.md

    # ... to add a Summary Consumer (ingesting output from `SummaryProducer`)
    md -p ../SummaryConsumer | Set-Location
    dotnet new summaryConsumer
    start README.md

    # ... to add a Testbed
    md -p ../My.Tools.Testbed | Set-Location
    # -e -c # add EventStore and CosmosDb suppport to got with the default support for MemoryStore
    dotnet new eqxtestbed -c -e
    start README.md
    # run for 1 min with 10000 rps against an in-memory store
    dotnet run -p Testbed -- run -d 1 -f 10000 memory
    # run for 30 mins with 2000 rps against a local EventStore
    dotnet run -p Testbed -- run -f 2000 es
    # run for two minutes against CosmosDb (see https://github.com/jet/equinox#quickstart) for provisioning instructions
    dotnet run -p Testbed -- run -d 2 cosmos

    # ... to add a Sync tool
    md -p ../My.Tools.Sync | Set-Location
    # (-m includes an example of how to upconvert from similar event-sourced representations in an existing store)
    dotnet new proSync -m
    start README.md

## TESTING

There's [no integration test for the templates yet](https://github.com/jet/dotnet-templates/issues/2), so validating a template is safe to release is unfortunately a manual process based on:

1. Generate the package (per set of changes you make locally)

    a. ensuring the template's base code compiles (see [runnable templates concept in `dotnet new` docs](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates))

    b. packaging into a local nupkg

        $ cd ~/dotnet-templates
        $ dotnet build build.proj
        Successfully created package '/Users/me/dotnet-templates/bin/nupkg/Equinox.Templates.3.10.1-alpha.0.1.nupkg'.

2. Test, per variant

    (Best to do this in another command prompt in a scratch area)

    a. installing the templates into the `dotnet new` local repo

        $ dotnet new -i /Users/me/dotnet-templates/bin/nupkg/Equinox.Templates.3.10.1-alpha.0.1.nupkg

    b. get to an empty scratch area

        $ mkdir -p ~/scratch/templs/t1
        $ cd ~/scratch/templs/t1
    
    c. test a variant (i.e. per `symbol` in the config)

        $ dotnet new proReactor -k # an example - in general you only need to test stuff you're actually changing
        $ dotnet build # test it compiles
        $ # REPEAT N TIMES FOR COMBINATIONS OF SYMBOLS

3. uninstalling the locally built templates from step 2a:

      $ dotnet new -u Equinox.Templates

Pssst ... the above is also what implementing [#2](https://github.com/jet/dotnet-templates/issues/2) involves!

# PATTERNS / GUIDANCE

## Strongly typed ids

- `FSharp.UMX` is useful to transparently pin types in a message contract cheaply

## Managing Projections and Reactions with Equinox, Propulsion and FsKafka

## Microservice Program.fs

All the templates herein attempt to adhere to a consistent structure for the root `module` (the one containing an Application’s `main`)

### `module Settings`

_Responsible for: Loading secrets and configuration into Environment variables_

Wiring up retrieval of configuration values is the most environment-dependent aspect of the wiring up of an applications data sources. This is particularly relevant where there is variance between local (development time), testing and production deployments. For this reason, the retrieval of values from configuration stores or key vaults is not managed directly within the [`CmdParser` section](#module-cmdparser)

The `Settings` module is responsible for the following:
1. Feeding defaults into process-local Environment Variables, _where those are not already supplied_
2. Encapsulating all bindings to Configuration or Secret stores (Vaults) in order that this does not have to be complected with the argument parsing or defaulting in `CmdParser`

- DO (sparingly) rely on inputs from the command line to drive the lookup process
- DONT log values (CmdParser’s Arguments wrappers should do that as applicable as part of the wire process)
- DONT perform redundant work to load values if they’ve already been supplied via Environment Variables

### `module CmdParser`

_Responsible for: mapping Environment Variables and Command Line `argv` to an `Arguments` model_

The `CmdParser` module fulfils three roles:

1. uses [Argu](http://fsprojects.github.io/Argu/tutorial.html) to map the inputs passed via `argv` to values per argument, providing good error and/or help messages in the case of invalid inputs
2. Is responsible for managing all defaulting of input values _including echoing them them such that an operator can infer the arguments in force_ without having to go look up defaults in a source control repo
3. Exposes an object model that the `build` or `start` functions can use to succinctly wire up the dependencies without needing to touch `Argu`, `Settings`, or any concrete Configuration or Secrets store technology

- DO log the values being applied, especially where defaulting is in play
- DONT log secrets
- DO take values via Argu or Environment Variables
- DONT mix in any application or settings specific logic (no retrieval of values, don’t make people read the boilerplate to see if this app has custom secrets retrieval)

### `module Logging`

_Responsible for applying logging config and setting up loggers for the application_

- DO allow overriding of log level via a command line argument and/or environment variable (by passing CmdParser.Arguments or values from it

#### example

```
module Logging =
    let initialize verbose =
	Log.Logger <- LoggerConfiguration(….)
```

### `start` function

The `start` function contains the specific wireup relevant to the infrastructure requirements of the microservice - it's the sole aspect that is not expected to adhere to a standard layout as prescribed in this section.

#### example

```
let start (args : CmdParser.Arguments) =
	…
	(yields a started application loop)
```

### `run`,  `main` functions

The `run` function formalizes the overall pattern. It is responsible for:

1. Managing the correct sequencing of the startup procedure, weaving together the above elements,
2. managing the emission of startup or abnormal termination messages to the console

- DONT alter the canonical form - the processing is in this exact order for a multitude of reasons
- DONT have any application specific wire within `run` - any such logic should live within the `start` function

#### example

```
let run argv =
    try let args = CmdParser.parse argv
        Logging.initialize args.Verbose
        Settings.initialize ()
        use consumer = start args
        consumer.AwaitCompletion() |> Async.RunSynchronously
        if consumer.RanToCompletion then 0 else 2
    with :? Argu.ArguParseException as e -> eprintfn "%s" e.Message; 1
        | CmdParser.MissingArg msg -> eprintfn "%s" msg; 1
        // If the handler throws, we exit the app in order to let an orchestrator flag the failure
        | e -> Log.Fatal(e, "Exiting"); 1

[<EntryPoint>]
let main argv =
    try run argv
    finally Log.CloseAndFlush()
```

## CONTRIBUTING

Please don't hesitate to [create a GitHub issue](https://github.com/jet/dotnet-templates/issues/new) for any questions so others can benefit from the discussion. For any significant planned changes or additions, please err on the side of [reaching out early](https://github.com/jet/dotnet-templates/issues/new) so we can align expectations - there's nothing more frustrating than having your hard work not yielding a mutually agreeable result ;)

See [the Equinox repo's CONTRIBUTING section](https://github.com/jet/equinox/blob/master/README.md#contributing) for general guidelines wrt how contributions are considered specifically wrt Equinox.

The following sorts of things are top of the list for the templates:

- Fixes for typos, adding of info to the readme or comments in the emitted code etc
- Small-scale cleanup or clarifications of the emitted code
- support for additional languages in the templates
- further straightforward starter projects

While there is no rigid or defined limit to what makes sense to add, it should be borne in mind that `dotnet new eqx*` is often going to be a new user's first interaction with Equinox and/or [asp]dotnetcore. Hence there's a delicate (and intrinsically subjective) balance to be struck between:

  1. simplicity of programming techniques used / beginner friendliness
  2. brevity of the generated code
  3. encouraging good design practices

  In other words, there's lots of subtlety to what should and shouldn't go into a template - so discussing changes before investing time is encouraged; and agreed changes will generally be rolled out across the repo
