# FifteenBelow.Json

## Overview

FifteenBelow.Json provides a set of `JsonConverter` types for the Newtonsoft.Json library, focused on providing _idiomatic_ serialization of common F# types. While Newtonsoft.Json is progressing native support for F#, we feel that the JSON structures emitted by these converters are slightly more human friendly (where possible).

Some trade-offs have been made between power and simplicity, and these are documented where they apply to each converter in the following sections.

## Usage

The converters should be added to an `IList<JsonConverter>` and set as Converters on `JsonSerializerSettings`, which can then be passed to the various serialization/deserialization methods available. The converters have no dependencies between them, so you can load only the ones which apply if desired. Examples given below use a JsonSerializerSettings like this:



## Available Converters

### Option<'T>

The `OptionConverter` supports the F# `Option<'T>` type. `Some 'T` will be serialized as `'T`, while `None` will be serialized as `null` (Newtonsoft.Json has settings to control the writing of `null` values to JSON).

Serializing:
```fsharp
type OptionType = { Option: string

let someType = { Option = Some "Hello World!" }
let noneType = { Option = None }

let someJson = JsonConvert.Serialize<OptionType> (someType, settings)
let noneJson = JsonConvert.Serialize<OptionType> (noneType, settings)
```

someJson:
```json
{ "option": "HelloWorld" }
```

noneJson:
```json
{}
```


