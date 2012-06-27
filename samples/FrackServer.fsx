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
#r @"..\packages\FSharpx.Core.1.6.4\lib\40\FSharpx.Core.dll"
#I @"..\src\Frack"
#load "Owin.fs"
#load "BufferPool.fs"
#load "Sockets.fs"
#load "Tcp.fs"
#load "Http.fs"

open FSharp.Control
open FSharpx
open Frack

let server = Http.Server(fun request -> async {
    System.Console.WriteLine("Request received")
    return {
        StatusCode = 200
        Headers = dict [| ("Content-Type", [|"text/plain"|]); ("Content-Length", [|"13"|]); ("Connection", [|"Close"|]) |]
        Body = asyncSeq { yield BS"Hello, world!"B }
        Properties = dict [||]
    }
})

// Demo
let disposable = server.Start(port = 8090)
System.Console.WriteLine("Listening on port 8090")
disposable.Dispose()
