//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
module Frack.Tcp

open System
open System.Net
open System.Net.Sockets
open System.Threading
open FSharp.Control
open Frack
open Frack.Sockets
open Fracture

type Server(handle, ?backlog) =
    let backlog = defaultArg backlog 10000
    let [<Literal>] defaultTimeout = 1000

    member x.Start(hostname:string, ?port, ?maxPoolCount, ?perBocketBufferSize) =
        let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
        x.Start(ipAddress, ?port = port, ?maxPoolCount = maxPoolCount, ?perBocketBufferSize = perBocketBufferSize)

    member x.Start(?ipAddress, ?port, ?maxPoolCount, ?perBocketBufferSize) =
        let ipAddress = defaultArg ipAddress IPAddress.Any
        let port = defaultArg port 80
        let maxPoolCount = defaultArg maxPoolCount 10000
        let perBocketBufferSize = defaultArg perBocketBufferSize 0
        let pool = new BocketPool("accept", maxPoolCount, perBocketBufferSize)
        let endpoint = IPEndPoint(ipAddress, port)
        let cts = new CancellationTokenSource()

        let listener =
            new Socket(
                AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp,
                ReceiveBufferSize = 16384,
                SendBufferSize = 16384,
                NoDelay = false, // This disables nagle on true
                LingerState = LingerOption(true, 2))
        listener.Bind(endpoint)
        listener.Listen(backlog)

        let log (e: exn) = Console.WriteLine(e)
        
        let runHandler (connection: Socket) =
            let finish comp = async {
                let! choice = comp
                let args = pool.CheckOut()
                do! connection.AsyncDisconnect(args)
                // TODO: Don't `Close` the connection; rather restore it to the socket pool.
                connection.Close()
                pool.CheckIn(args)
                match choice with
                | Choice1Of2 () -> ()
                | Choice2Of2 (e: exn) ->
                    System.Diagnostics.Trace.TraceError("{0}", e)
            }
            handle connection
            |> Async.Catch
            |> finish

        let runServer () = async {
            // TODO: Start the number of concurrent connections desired only.
            while true do
                let args = pool.CheckOut()
                // TODO: Pool the sockets, retrieve one here, and assign it to the `args`.
                // TODO: A blocking queue should allow us to define the number of connections and still run within the `while`.
                let! connection = listener.AsyncAccept(args)
                // TODO: Verify the returned `connection` is the same as the one assigned.
                // TODO: Remove the returned `connection` from the `args` before checking in.
                args.AcceptSocket <- null
                pool.CheckIn(args)
                connection.ReceiveTimeout <- defaultTimeout
                connection.SendTimeout <- defaultTimeout
                // TODO: Throttle this, as it will otherwise run away and fail ... very quickly.
                //Async.StartWithContinuations(runHandler connection, ignore, log, log, cts.Token)
                // NOTE: This should run one connection at a time.
                do! runHandler connection
        }

        Async.StartWithContinuations(runServer (), ignore, log, log, cancellationToken = cts.Token)
        { new IDisposable with
            member x.Dispose() =
                cts.Cancel()
                if listener <> null then
                    listener.Close(2)
                pool.Dispose()
        }
