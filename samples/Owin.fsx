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
open Owin

let source = asyncSeq {
    yield BS@"GET / HTTP/1.1
Host: wizardsofsmart.net
Accept: application/json

"B
}

//----------------------------------------------------------------------------
// Http.Parser
#time
let request = Http.Parser.parse source |> Async.RunSynchronously
#time

//----------------------------------------------------------------------------
// Http.Parser + read body
#time
async {
    let! request = Http.Parser.parse source
    let! body = request.Body |> AsyncSeq.fold ByteString.append ByteString.empty
    return request, body
} |> Async.RunSynchronously
#time

//----------------------------------------------------------------------------
// Run an OWIN application
let app request = async {
    return {
        StatusCode = 200
        Headers = dict [| ("Content-Type", [|"text/plain"|]); ("Content-Length", [|"13"|]) |]
        Body = asyncSeq { yield BS"Hello, world!"B }
        Properties = dict [||]
    }
}

#time
async {
    let! request = Http.Parser.parse source
    return! app request
} |> Async.RunSynchronously
#time

//----------------------------------------------------------------------------
// Run an OWIN application + read the response body
#time
async {
    let! request = Http.Parser.parse source
    let! response = app request
    let! body = response.Body |> AsyncSeq.fold ByteString.append ByteString.empty
    return response, body |> ByteString.toString
} |> Async.RunSynchronously
#time

//----------------------------------------------------------------------------
// Run an OWIN application + transform the response to bytes
#time
async {
    let! request = Http.Parser.parse source
    let! response = app request
    let! bytes = Response.toBytes response |> AsyncSeq.fold ByteString.append ByteString.empty
    return bytes |> ByteString.toString
} |> Async.RunSynchronously
#time
