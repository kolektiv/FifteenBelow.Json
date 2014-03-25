namespace FifteenBelow.Json

open System


[<AutoOpen>]
module internal Reader =

    type Reader<'R,'T> = 'R -> 'T

    let bind k m = fun r -> (k (m r)) r

    let inline flip f a b = f b a
    
    type ReaderBuilder () =

        member this.Return (a) : Reader<'R,'T> = fun _ -> a

        member this.ReturnFrom (a: Reader<'R,'T>) = a

        member this.Bind (m: Reader<'R,'T>, k:'T -> Reader<'R,'U>) : Reader<'R,'U> = 
            bind k m

        member this.Zero () = 
            this.Return ()

        member this.Combine (r1, r2) = 
            this.Bind (r1, fun () -> r2)

        member this.TryWith (m: Reader<'R,'T>, h: exn -> Reader<'R,'T>) : Reader<'R,'T> =
            fun env -> try m env
                        with e -> (h e) env

        member this.TryFinally (m: Reader<'R,'T>, compensation) : Reader<'R,'T> =
            fun env -> try m env
                        finally compensation()

        member this.Using (res: #IDisposable, body) =
            this.TryFinally (body res, (fun () -> match res with null -> () | disp -> disp.Dispose ()))

        member this.Delay (f) = 
            this.Bind (this.Return (), f)

        member this.While (guard, m) =
            if not (guard ()) then 
                this.Zero () 
            else
                this.Bind (m, (fun () -> this.While (guard, m)))

        member this.For(sequence: seq<_>, body) =
            this.Using (sequence.GetEnumerator (),
                (fun enum -> this.While(enum.MoveNext, this.Delay(fun () -> body enum.Current))))
