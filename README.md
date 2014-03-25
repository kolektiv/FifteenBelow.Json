# FifteenBelow.Json

## Overview

FifteenBelow.Json provides a set of `JsonConverter` types for the Newtonsoft.Json library, focused on providing _idiomatic_ serialization of common F# types. While Newtonsoft.Json is progressing native support for F#, we feel that the JSON structures emitted by these converters are slightly more human friendly (where possible).

Some trade-offs have been made between power and simplicity, and these are documented where they apply to each converter in the following sections. While the examples only show F# -> JSON, deserialization works as expected.

## Usage

The converters should be added to an `IList<JsonConverter>` and set as Converters on `JsonSerializerSettings`, which can then be passed to the various serialization/deserialization methods available. The converters have no dependencies between them, so you can load only the ones which apply if desired. Examples given below use a JsonSerializerSettings like this:

```fsharp
let converters =
    [ OptionConverter () :> JsonConverter
      TupleConverter () :> JsonConverter
      ListConverter () :> JsonConverter
      MapConverter () :> JsonConverter
      BoxedMapConverter () :> JsonConverter
      UnionConverter () :> JsonConverter ] |> List.toArray :> IList<JsonConverter>

let settings =
    JsonSerializerSettings (
        ContractResolver = CamelCasePropertyNamesContractResolver (), 
        Converters = converters,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore)
```

## Available Converters

### Options

The `OptionConverter` supports the F# `Option` type. `Some 'T` will be serialized as `'T`, while `None` will be serialized as `null` (Newtonsoft.Json has settings to control the writing of `null` values to JSON).

```fsharp
type OptionType = { Option: string }

let someType = { Option = Some "Hello World!" }
let someJson = JsonConvert.Serialize<OptionType> (someType, settings)

let noneType = { Option = None }
let noneJson = JsonConvert.Serialize<OptionType> (noneType, settings)
```

```js
// someJson
{
  "option": "Hello World"
}

// noneJson
{}
```

### Tuples

The `TupleConverter` supports the F# Tuple type. As Tuples are essentially positional (and heterogeneous) data structures, they are serialized as JSON arrays (as arrays in JSON can contain heterogeneous types natively).

```fsharp
type TupleType = { Tuple: string * int * bool }

let tupleType = { Tuple = "Hello", 5, true }
let tupleJson = JsonConvert.Serialize<TupleType> (tupleType, settings)
```

```js
// tupleJson
{
  "tuple": [
    "Hello",
    5,
    true
  ]
}
```
