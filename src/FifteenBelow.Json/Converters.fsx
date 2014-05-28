#r "bin/Debug/Newtonsoft.Json.dll"

#load "Reader.fs"

open FifteenBelow.Json
#load "Converters.fs"

open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open FifteenBelow.Json

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

type Unions =
    | One
    | Two of string
    | Rec of Unions

type Test =
    { Name : string option
      Number : int
      Lists : string list
      Items : Map<string, int>
      Things : Unions * Unions }

let value = 
    { Name = Some "test"
      Number = 99
      Lists = ["asdf"]
      Items = ["one", 1] |> Map.ofList
      Things = One, Two "" }


let x = JsonConvert.SerializeObject(value, settings)
JsonConvert.DeserializeObject<Test>(x, settings)

let value' = 
    { Name = None 
      Number = -99
      Lists = []
      Items = Map.empty 
      Things = One, Rec One }

let x' = JsonConvert.SerializeObject(value', settings)
JsonConvert.DeserializeObject<Test>(x', settings)
