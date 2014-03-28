namespace FifteenBelow.Json

open System
open System.Collections.Generic
open System.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Serialization


[<RequireQualifiedAccess>]
module Utils =

    let uninstallDefaultConverter (converter: Type) =
        let flags = BindingFlags.NonPublic ||| BindingFlags.Static
        let convertersInfo = typeof<DefaultContractResolver>.GetMember ( "BuiltInConverters", flags)
        let converters = (convertersInfo.[0] :?> FieldInfo).GetValue (null) :?> List<JsonConverter>

        match converters.FindIndex (fun x -> x.GetType () = converter) with
        | x when (x > -1) -> converters.RemoveAt (x)
        | _ -> ()

    let uninstallDefaultUnionConverter () =
        uninstallDefaultConverter (typeof<DiscriminatedUnionConverter>)
