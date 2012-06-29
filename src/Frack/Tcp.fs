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

type Server(handle, ?backlog) =
    let backlog = defaultArg backlog 1000

    member x.Start(hostname:string, ?port) =
        let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
        x.Start(ipAddress, ?port = port)

    member x.Start(?ipAddress, ?port) =
        let ipAddress = defaultArg ipAddress IPAddress.Any
        let port = defaultArg port 80
        let endpoint = IPEndPoint(ipAddress, port)
        let cts = new CancellationTokenSource()

        let listener = Sockets.createTcpSocket()
        listener.Bind(endpoint)
        listener.Listen(backlog)
        
        let runHandler (connection: Socket) =
            let finish comp = async {
                let! choice = comp
                do! connection.AsyncDisconnect()
                match choice with
                | Choice1Of2 () -> ()
                | Choice2Of2 (e: exn) ->
                    System.Diagnostics.Trace.TraceError("{0}", e)
            }
            handle connection
            |> Async.Catch
            |> finish

        let log (e: exn) = Console.WriteLine("{0}", e)

        let runServer () = async {
            for connection : Socket in listener.AsyncAcceptSeq() do
                Async.StartWithContinuations(runHandler connection, ignore, log, log, cts.Token)
        }

        Async.StartImmediate(runServer (), cancellationToken = cts.Token)
        { new IDisposable with
            member x.Dispose() =
                cts.Cancel()
                if listener <> null then
                    listener.Close(2)
        }
