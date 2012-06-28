// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
module Frack.Sockets
#nowarn "40"

open System
open System.Net.Sockets
open Frack
open FSharp.Control
open FSharpx

type A = SocketAsyncEventArgs
type BS = ByteString

exception SocketIssue of SocketError with
    override this.ToString() = string this.Data0

/// Wraps the Socket.xxxAsync logic into F# async logic.
let asyncDo op prepare select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let args = new A()
        prepare args
        let k (args: A) =
            match args.SocketError with
            | SocketError.Success ->
                args.Dispose()
                ok <| select args
            | e ->
                args.Dispose()
                error <| SocketIssue e
        let rec finish cont value =
            remover.Dispose()
            cont value
        and remover : IDisposable =
            args.Completed.Subscribe
                ({ new IObserver<_> with
                    member x.OnNext(v) = finish k v
                    member x.OnError(e) = finish error e
                    member x.OnCompleted() =
                        finish cancel <| System.OperationCanceledException("Cancelling the workflow, because the Observable awaited has completed.")
                })
        if not (op args) then
            finish k args

/// Prepares the arguments by setting the buffer.
let inline setBuffer (buf: BS) (args: A) =
    args.SetBuffer(buf.Array, buf.Offset, buf.Count)

let private bytesPerLong = 4
let private bitsPerByte = 8

type Socket with
    member x.SetKeepAlive(time, interval) =
        try
            // Array to hold input values
            let input = [| (if time = 0UL || interval = 0UL then 0UL else 1UL); time; interval |]
            // Pack input into byte struct
            let inValue = Array.zeroCreate (3 * bytesPerLong)
            for i in 0..input.Length - 1 do
                inValue.[i * bytesPerLong + 3] <- Convert.ToByte(input.[i] >>> ((bytesPerLong - 1) * bitsPerByte) &&& 0xffUL)
                inValue.[i * bytesPerLong + 2] <- Convert.ToByte(input.[i] >>> ((bytesPerLong - 2) * bitsPerByte) &&& 0xffUL)
                inValue.[i * bytesPerLong + 1] <- Convert.ToByte(input.[i] >>> ((bytesPerLong - 3) * bitsPerByte) &&& 0xffUL)
                inValue.[i * bytesPerLong + 0] <- Convert.ToByte(input.[i] >>> ((bytesPerLong - 4) * bitsPerByte) &&& 0xffUL)
            // Create bytestruct for result (bytes pending on server socket).
            let outValue = BitConverter.GetBytes(0)
            // Write SIO_VALS to Socket.IOControl.
            x.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.KeepAlive, true)
            x.IOControl(IOControlCode.KeepAliveValues, inValue, outValue) |> ignore
            true
        with
        | :? SocketException as e -> false

    member x.AsyncAccept () =
        asyncDo x.AcceptAsync ignore (fun a -> a.AcceptSocket)

    member x.AcceptAsyncSeq () =
        let rec loop () = asyncSeq {
            let! socket = x.AsyncAccept()
            yield socket
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive (buf: BS) =
        asyncDo x.ReceiveAsync (setBuffer buf) (fun a -> a.BytesTransferred)

    member x.ReceiveAsyncSeq (pool: BufferPool) =
        let rec loop () = asyncSeq {
            let! buf = pool.Pop()
            let! bytesRead = x.AsyncReceive(buf)
            if bytesRead > 0 then
                let chunk = BS(buf.Array.[buf.Offset..buf.Offset + bytesRead])
                do! pool.Push(buf)
                yield chunk
                yield! loop ()
            else
                do! pool.Push(buf)
                ()
        }
        loop ()

    member x.AsyncSend (buf: BS) =
        asyncDo x.SendAsync (setBuffer buf) ignore

    member x.SendAsyncSeq (data, pool: BufferPool) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                let! buf = pool.Pop()
                System.Buffer.BlockCopy(bs.Array, bs.Offset, buf.Array, buf.Offset, bs.Count)
                do! x.AsyncSend(BS(buf.Array, buf.Offset, bs.Count))
                do! pool.Push(buf)
                do! loop rest
            | Nil -> ()
        }
        loop data

    member x.AsyncDisconnect () =
        asyncDo x.DisconnectAsync ignore <| fun a ->
            try
                x.Shutdown(SocketShutdown.Send)
            finally
                x.Close()
