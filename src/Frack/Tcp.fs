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

type Server(f, ?backlog) =
    let backlog = defaultArg backlog 1000

    member x.Start(hostname:string, ?port) =
        let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
        x.Start(ipAddress, ?port = port)

    member x.Start(?ipAddress, ?port) =
        let ipAddress = defaultArg ipAddress IPAddress.Any
        let port = defaultArg port 80
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
        
        let close (socket: Socket) () =
            socket.Shutdown(SocketShutdown.Both)
            socket.Close()

        let run () = async {
            for connection : Socket in listener.AcceptAsyncSeq() do
                Async.StartWithContinuations(f connection,
                    (fun keepAlive -> if keepAlive then close connection () else close connection ()),
                    printfn "%A" >> close connection,
                    printfn "%A" >> close connection,
                    cts.Token)
        }

        Async.Start(run (), cancellationToken = cts.Token)
        { new IDisposable with
            member x.Dispose() =
                cts.Cancel()
                if listener <> null then
                    listener.Close(2)
        }
