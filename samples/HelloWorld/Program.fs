﻿//----------------------------------------------------------------------------
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

open FSharp.Control
open FSharpx
open Frack

[<EntryPoint>]
let main argv = 

    let server = Http.Server(fun request -> async {
        return {
            StatusCode = 200
            Headers = dict [| ("Content-Type", [|"text/plain"|]); ("Content-Length", [|"13"|]); ("Connection", [|"Close"|]) |]
            Body = asyncSeq { yield BS"Hello, world!"B }
            Properties = null
        }
    })

    let disposable = server.Start(port = 8090)

    System.Console.WriteLine("Listening on port 8090")
    System.Console.WriteLine("Press Enter to quit.")
    System.Console.ReadLine() |> ignore

    disposable.Dispose()
    0