module Frack.Sockets
#nowarn "40"

open System
open System.Net.Sockets
open Fracture
open FSharp.Control
open FSharpx

// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
type A = SocketAsyncEventArgs
type B = BocketPool
type BS = ByteString

/// Wraps the Socket.xxxAsync logic into F# async logic.
let inline asyncDo op (args: A) select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let callback (args: A) =
            if args.SocketError = SocketError.Success then
                let result = select args
                args.Dispose()
                ok result
            else
                args.Dispose()
                error (SocketException(int args.SocketError))
        args.Completed.Add(callback)
        if not (op args) then
            callback args

type Socket with
    member x.AsyncAccept args = asyncDo x.AcceptAsync args (fun a -> a.AcceptSocket)
    member x.AsyncReceive args = asyncDo x.ReceiveAsync args (fun a -> a.BytesTransferred)
    member x.AsyncSend args = asyncDo x.SendAsync args ignore
    member x.AsyncDisconnect args =
        asyncDo x.DisconnectAsync args <| fun a ->
            try
                x.Shutdown(SocketShutdown.Both)
            finally
                x.Close()

/// A read-only stream wrapping a `Socket`.
type SocketReadStream(socket: Socket, pool: B) as x =
    inherit System.IO.Stream()
    do if socket = null then
        raise <| ArgumentNullException()
    do if not (socket.Blocking) then
        raise <| System.IO.IOException()
    do if not (socket.Connected) then
        raise <| System.IO.IOException()
    do if socket.SocketType <> SocketType.Stream then
        raise <| System.IO.IOException()

    // TODO: This type should probably also accept an additional, optional buffer as the first bit of data returned from a `Read`.
    // This would allow something like an HTTP parser to restore to the `SocketReadStream` any bytes read that were not part of
    // the HTTP headers.

    let mutable readTimeout = 1000
    let beginRead, endRead, _ = Async.AsBeginEnd(fun (b, o, c) -> x.AsyncRead(b, o, c))

    /// Reads `count` bytes from the specified `buffer` starting at the specified `offset` asynchronously.
    /// It's possible that the caller is providing a different buffer, offset, or count.
    /// The `BocketPool` is highly optimized to use a single, large buffer and limit
    /// extraneous calls to methods such as `SocketAsyncEventArgs.SetBuffer`. When possible,
    /// the caller should provide the same values as were used to create the `BocketPool`.
    member x.AsyncRead(buffer: byte[], offset, count) =
        if count = 0 then async.Return 0 else
    
        // TODO: This type should retain the `args` whenever the caller's offset + count does not retrieve the bytes
        // stored in the `args`. Subsequent calls should then first read from the previous args, then retrieve the next.

        async {
            let args = pool.CheckOut()
            if buffer = args.Buffer && offset = args.Offset && count = args.Count then
                // The happy path is if the caller uses the optimized settings,
                // meaning that the caller passes the buffer, offset, and count
                // used for the existing buffer.
                let! bytesRead = socket.AsyncReceive(args)
                pool.CheckIn(args)
                return bytesRead
            else
                // Check that the buffer, offset, and count are valid.
                if buffer = null then
                    raise <| ArgumentNullException()
                if offset < 0 || offset >= buffer.Length then
                    raise <| ArgumentOutOfRangeException("offset")
                if count < 0 || count > buffer.Length - offset then
                    raise <| ArgumentOutOfRangeException("count")

                // Capture the original values.
                let origBuffer = args.Buffer
                let origOffset = args.Offset
                let origCount = args.Count

                // Set the args buffer to the requested buffer, offset, and count.
                args.SetBuffer(buffer, offset, count)
                let! bytesRead = socket.AsyncReceive(args)

                // Restore the original values to the args.
                args.SetBuffer(origBuffer, origOffset, origCount)
                pool.CheckIn(args)
                return bytesRead
        }

    override x.CanRead = false
    override x.CanSeek = false
    override x.CanWrite = true
    override x.Flush() = raise <| NotSupportedException()
    override x.Length = raise <| NotSupportedException()
    override x.Position
        with get() = raise <| NotSupportedException()
        and set(v) = raise <| NotSupportedException()
    override x.ReadTimeout
        with get() = readTimeout
        and set(v) = readTimeout <- v
    override x.Seek(offset, origin) = raise <| NotSupportedException()
    override x.SetLength(value) = raise <| NotSupportedException()
    override x.Read(buffer, offset, count) =
        x.AsyncRead(buffer, offset, count)
        |> Async.RunSynchronously
    override x.Write(buffer, offset, count) = raise <| NotSupportedException()

    // Async Stream methods

#if NET45
    override x.ReadAsync(buffer, offset, count, cancellationToken) =
        Async.StartAsTask(
            x.AsyncRead(buffer, offset, count),
            cancellationToken = cancellationToken)
#endif
    override x.BeginRead(buffer, offset, count, callback, state) =
        beginRead((buffer, offset, count), callback, state)
    override x.EndRead(asyncResult) = endRead(asyncResult)

/// A write-only stream wrapping a `Socket`.
type SocketWriteStream(socket: Socket, pool: B) as x =
    inherit System.IO.Stream()
    do if socket = null then
        raise <| ArgumentNullException()
    do if not (socket.Blocking) then
        raise <| System.IO.IOException()
    do if not (socket.Connected) then
        raise <| System.IO.IOException()
    do if socket.SocketType <> SocketType.Stream then
        raise <| System.IO.IOException()
    
    let mutable writeTimeout = 1000
    let beginWrite, endWrite, _ = Async.AsBeginEnd(fun (b, o, c) -> x.AsyncWrite(b, o, c))

    /// Asynchronously writes `count` bytes to the `socket` starting at the specified `offset`
    /// from the provided `buffer`.
    member x.AsyncWrite(buffer: byte[], offset, count) =
        if buffer = null then
            raise <| ArgumentNullException()
        if offset < 0 || offset >= buffer.Length then
            raise <| ArgumentOutOfRangeException("offset")
        if count < 0 || count > buffer.Length - offset then
            raise <| ArgumentOutOfRangeException("count")

        if count = 0 then async.Zero () else

        async {
            let args = pool.CheckOut()
            // Copy the data into the args buffer.
            Buffer.BlockCopy(buffer, offset, args.Buffer, args.Offset, count)
            // TODO: Handle the case where the args.Count is too small
            do! socket.AsyncSend(args)
            pool.CheckIn(args)
        }

    override x.CanRead = true
    override x.CanSeek = false
    override x.CanWrite = false
    override x.Flush() = raise <| NotSupportedException()
    override x.Length = raise <| NotSupportedException()
    override x.Position
        with get() = raise <| NotSupportedException()
        and set(v) = raise <| NotSupportedException()
    override x.Seek(offset, origin) = raise <| NotSupportedException()
    override x.WriteTimeout
        with get() = writeTimeout
        and set(v) = writeTimeout <- v
    override x.SetLength(value) = raise <| NotSupportedException()
    override x.Read(buffer, offset, count) = raise <| NotSupportedException()
    override x.Write(buffer, offset, count) =
        x.AsyncWrite(buffer, offset, count)
        |> Async.RunSynchronously

    // Async Stream methods

#if NET45
    override x.WriteAsync(buffer, offset, count, cancellationToken) =
        Async.StartAsTask(
            x.AsyncWrite(buffer, offset, count),
            cancellationToken = cancellationToken
        )
        :> System.Threading.Tasks.Task
#endif
    override x.BeginWrite(buffer, offset, count, callback, state) =
        beginWrite((buffer, offset, count), callback, state)
    override x.EndWrite(asyncResult) = endWrite(asyncResult)