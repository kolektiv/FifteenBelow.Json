namespace FifteenBelow.Json

open System
open System.Collections
open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Serialization


[<AutoOpen>]
module internal State =

    type JsonState =
        { Reader: JsonReader option
          Writer: JsonWriter option
          Serializer: JsonSerializer }
 
        static member read reader serializer =
            { Reader = Some reader
              Writer = None
              Serializer = serializer }
 
        static member write writer serializer =
            { Reader = None
              Writer = Some writer
              Serializer = serializer }

    let json = ReaderBuilder ()

    let read func =
        json {
            return! (fun x -> func x.Serializer x.Reader.Value) }

    let write func =
        json {
            return! (fun x -> func x.Serializer x.Writer.Value) }


[<AutoOpen>]
module internal Common =

    let property o name =
        o.GetType().GetProperty(name).GetValue(o, null)
 
    let objKey o =
        property o "Key"
 
    let objValue o =
        property o "Value"

    let tokenType () =
        json {
            return! read (fun _ r -> 
                r.TokenType) }
 
    let ignore () =
        json {
            do! read (fun _ r -> 
                r.Read () |> ignore) }

    let value () =
        json {
            return! read (fun _ r -> 
                r.Value) }

    let serialize (o: obj) =
        json {
            do! write (fun s w -> 
                s.Serialize (w, o)) }
 
    let deserialize (t: Type) =
        json {
            return! read (fun s r -> 
                s.Deserialize (r, t)) }

    let mapName (n: string) =
        json {
            return! write (fun s _ ->
                (s.ContractResolver :?> DefaultContractResolver).GetResolvedPropertyName (n)) }

    let readArray next =
        json {
            let! tokenType = flip tokenType
            let! ignore = flip ignore
            let! deserialize = flip deserialize
 
            let rec read index data =
                match tokenType () with
                | JsonToken.StartArray ->
                    ignore ()
                    read index data
                | JsonToken.EndArray ->
                    data
                | _ ->
                    let value = deserialize (next (index))
                    ignore ()
                    read (index + 1) (data @ [value])
 
            return read 0 List.empty |> Array.ofList }

    let readObject func keyType valueType =
        json {
            let! tokenType = flip tokenType
            let! ignore = flip ignore
            let! value = flip value
            let! deserialize = flip deserialize

            let key =
                match keyType with
                | t when t = typeof<string> -> fun o -> box (string o)
                | t when t = typeof<Guid> -> fun o -> box (Guid (string o))
                | t when t = typeof<int> -> fun o -> box (System.Int32.Parse o)
                | _ -> failwith "key type not allowed"
 
            let rec read data =
                match tokenType () with
                | JsonToken.StartObject ->
                    ignore ()
                    read data
                | JsonToken.EndObject ->
                    data
                | _ ->
                    let k = key (string (value ()))
                    ignore ()
                    let v = deserialize valueType
                    ignore ()
                    read (func k v :: data)
            
            return read List.empty }
 
    let writeObject (map: Map<string, obj>) =
        json {
            do! write (fun _ w -> w.WriteStartObject ())
        
            for pair in map do
                do! write (fun _ w -> w.WritePropertyName (pair.Key))
                do! write (fun s w -> s.Serialize (w, pair.Value))
 
            do! write (fun _ w -> w.WriteEndObject ()) }


[<AutoOpen>] 
module internal Options =

    let isOption (t: Type) =
        t.IsGenericType && t.GetGenericTypeDefinition () = typedefof<option<_>>
 
    let readOption (t: Type) =
        json {
            let cases = FSharpType.GetUnionCases (t)
            let args = t.GetGenericArguments ()
 
            let optionOf =
                match args.[0].IsValueType with
                | true -> (typedefof<Nullable<_>>).MakeGenericType ([| args.[0] |])
                | _ -> args.[0]
 
            let! result = deserialize optionOf
 
            return
                match result with
                | null -> FSharpValue.MakeUnion (cases.[0], [||])
                | value -> FSharpValue.MakeUnion (cases.[1], [| value |]) }
 
    let writeOption (o: obj) =
        json {
            do! serialize ((snd (FSharpValue.GetUnionFields (o, o.GetType ()))).[0]) }


[<AutoOpen>]
module internal Tuples =

    let isTuple (t: Type) =
        FSharpType.IsTuple t
 
    let readTuple (t: Type) =
        json {
            let types = FSharpType.GetTupleElements (t)
            let! values = readArray (fun i -> types.[i])
 
            return FSharpValue.MakeTuple (values, t) }
 
    let writeTuple o =
        json {
            do! serialize (FSharpValue.GetTupleFields (o)) }


[<AutoOpen>]
module internal Lists =

    let isList (t: Type) =
        t.IsGenericType && t.GetGenericTypeDefinition () = typedefof<list<_>>
    
    let readList (t: Type) =
        json {
            let itemType = t.GetGenericArguments().[0]
            let cases = FSharpType.GetUnionCases (typedefof<list<_>>.MakeGenericType (itemType))
 
            let rec make = 
                function
                | head :: tail -> FSharpValue.MakeUnion (cases.[1], [| head; (make tail); |])
                | [] -> FSharpValue.MakeUnion (cases.[0], [||])   
            
            let! array = readArray (fun _ -> itemType)         
                
            return
                array
                |> Seq.toList
                |> make }
 
    let writeList (o: obj) =
        json {
            do! serialize (Array.ofSeq (Seq.cast (o :?> IEnumerable))) }


[<AutoOpen>]
module internal Maps =

    let makeArray t (data: obj list) =
        let array = Array.CreateInstance (t, data.Length)
 
        data |> List.iteri (fun i item -> array.SetValue (item, i))
        array
 
    let makeMap (args: Type []) data =
        typeof<Map<_,_>>
            .Assembly
            .GetType("Microsoft.FSharp.Collections.MapModule")
            .GetMethod("OfArray")
            .MakeGenericMethod([| args.[0]; args.[1] |])
            .Invoke(null, [| data |])

    let allowedKey (t: Type) =
        t = typeof<string> || t = typeof<Guid> || t = typeof<int>
 
    let isMap (t: Type) =
        t.IsGenericType 
            && t.GetGenericTypeDefinition () = typedefof<Map<_,_>>
            && allowedKey (t.GetGenericArguments().[0])
            && t.GetGenericArguments().[1] <> typedefof<obj>
 
    let readMap (t: Type) =
        json {
            let args = t.GetGenericArguments ()
            let tupleType = FSharpType.MakeTupleType [| args.[0]; args.[1] |]
 
            let! data = readObject (fun k v -> 
                FSharpValue.MakeTuple ([| k; v |], tupleType)) args.[0] args.[1]
 
            return makeArray tupleType data |> makeMap args }
 
    let writeMap (o: obj) =
        json {
            let properties =
                o :?> IEnumerable
                |> Seq.cast
                |> Seq.map (fun x -> string (objKey x), objValue x)
                |> Map.ofSeq
 
            do! writeObject properties }

    let isBoxedMap (t: Type) =
        t.IsGenericType 
            && t.GetGenericTypeDefinition () = typedefof<Map<_,_>>
            && allowedKey (t.GetGenericArguments().[0])
            && t.GetGenericArguments().[1] = typedefof<obj>

    let typeKey = "$type"
    let valueKey = "value"

    let readBoxedMap (t: Type) =
        json {
            let args = t.GetGenericArguments ()
            let tupleType = FSharpType.MakeTupleType [| args.[0]; args.[1] |]

            let! s = (fun x -> x.Serializer)

            let! data = readObject (fun k v -> 
                let o = v :?> Linq.JObject
                let t = Type.GetType (string (o.Item(typeKey)))
                let v = s.Deserialize (o.Item(valueKey).CreateReader (), t)

                FSharpValue.MakeTuple ([| k; v |], tupleType)) args.[0] typeof<Linq.JObject>

            return makeArray tupleType data |> makeMap args }

    let writeBoxedMap (o: obj) =
        json {
            let properties =
                o :?> IEnumerable
                |> Seq.cast
                |> Seq.map (fun x -> string (objKey x), objValue x)
                |> Map.ofSeq

            do! write (fun _ w -> w.WriteStartObject ())

            for pair in properties do
                let value =
                    [ typeKey, box (pair.Value.GetType().FullName)
                      valueKey, box pair.Value ] |> Map.ofList
                do! write (fun _ w -> w.WritePropertyName (pair.Key))
                do! writeObject (value)

            do! write (fun _ w -> w.WriteEndObject ()) }


[<AutoOpen>]
module internal Unions =

    let isUnion (t: Type) =
        FSharpType.IsUnion t && not (isList t)
 
    let readUnion (t: Type) =
        json {
            do! ignore ()
            let! caseName = value ()
            do! ignore ()
            
            let case =  FSharpType.GetUnionCases (t) |> Array.find (fun x -> String.Equals (string caseName, x.Name, StringComparison.OrdinalIgnoreCase))
            let types = case.GetFields () |> Array.map (fun f -> f.PropertyType)
            let! array = readArray (fun i -> types.[i])
            let union = FSharpValue.MakeUnion (case, array)
            
            do! ignore ()
            
            return union }
 
    let writeUnion (o: obj) =
        json {
            let case, fields = FSharpValue.GetUnionFields (o, o.GetType ())
            let! caseName = mapName case.Name
            let properties = [caseName, box fields] |> Map.ofList

            do! writeObject (properties) }


type OptionConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isOption t
    override x.ReadJson (r, t, _, s) = readOption t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeOption v (JsonState.write w s)
 
type TupleConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isTuple t
    override x.ReadJson (r, t, _, s) = readTuple t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeTuple v (JsonState.write w s)
 
type ListConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isList t
    override x.ReadJson (r, t, _, s) = readList t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeList v (JsonState.write w s)
 
type MapConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isMap t
    override x.ReadJson (r, t, _, s) = readMap t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeMap v (JsonState.write w s)

type BoxedMapConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isBoxedMap t
    override x.ReadJson (r, t, _, s) = readBoxedMap t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeBoxedMap v (JsonState.write w s)

type UnionConverter () =
    inherit JsonConverter ()
    override x.CanConvert (t) = isUnion t
    override x.ReadJson (r, t, _, s) = readUnion t (JsonState.read r s)
    override x.WriteJson (w, v, s) = writeUnion v (JsonState.write w s)
