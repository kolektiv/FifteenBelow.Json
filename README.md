# FifteenBelow.Json

## Overview

FifteenBelow.Json provides a set of `JsonConverter` types for the Newtonsoft.Json library, focused on providing _idiomatic_ serialization of common F# types. While Newtonsoft.Json is progressing native support for F#, we feel that the JSON structures emitted by these converters are slightly more human friendly (where possible).

Some trade-offs have been made between power and simplicity, and these are documented where they apply to each converter in the following sections. While the examples only show F# -> JSON, deserialization works as expected.

## Installation

FifteenBelow.Json is available on NuGet, with the package name __FifteenBelow.Json__.

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

## Included Converters

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

### Lists

The `ListConverter` supports the F# List type. Lists are serialized as homogeneous JSON arrays.

```fsharp
type ListType = { List: string list }

let listType = { List = ["Hello"; "World!"] }
let listJson = JsonConvert.Serialize<ListType> (listType, settings)
```

```js
// listJson
{
  "list": [
    "Hello",
	"World!"
  ]
}
```

### Maps

The `MapConverter` supports the F# Map type, with the proviso that the map is of Type `Map<string,'T>`, where `'T` is not `obj`. The `BoxedMapConverter` supports maps of `Map<string,obj>`. This is an intentional design decision, as non-string keys don't map to JSON well. While it's possible to support a finite set of other key types which would have sensible string representations, the decision was made that it's better and more predictable to restrict the key type to `String` and convert other representations programatically on serialization/deserialization.

The `MapConverter` converts a map to a JSON object, while the `BoxedMapConverter` converts to a JSON object where each value is an object containing the type of the object and it's value.

```fsharp
type MapType =
	{ Map: Map<string,int>
	  BoxedMap: Map<string,obj> }
	  
let mapType =
	{ Map = [ "foo", 10; "bar", 20 ] |> Map.ofList
	  BoxedMap = [ "foo", box 10; "bar", box "twenty" ] |> Map.ofList }
let mapJson = JsonConvert.Serialize<MapType> (mapType, settings)
```

```js
// mapJson
{
  "map": {
    "foo": 10
	"bar": 20
  },
  "boxedMap": {
    "foo": {
      "$type": "System.Int",
	  "value": 10
    },
	"bar": {
	  "$type": "System.String",
	  "value": "twenty"
	}
  }
}
```

### Unions

The `UnionConverter` supports F# Discriminated Unions. As Union Case types are essentially positional, individual cases are serialized as heterogeneous JSON arrays, within an object, where the key will be the case name. This decision was made to allow simple JavaScript, checking for the presence of a key within the serialized union object as a very basic form of pattern matching. Note that case names will respect the contract resolver setting about key names (with these settings, they will be camel cased).

Also note that as cases are resolved by name, changes to the naming of a Union case between serialization and deserialization will be likely to cause issues.

```fsharp
type Union =
| First of string * int
| Second of bool * int

type UnionType = { Union: Union }

let unionType = { Union = First ("foo", 10) }
let unionJson = JsonConvert.Serialize<UnionType> (unionType, settings)
```

```js
// unionJson
{
  "union": {
    "first": [
      "foo",
	  10
	]
  }
}
```
