module Frack.AssemblyInfo
#nowarn "49" // uppercase argument names
#nowarn "67" // this type test or downcast will always hold
#nowarn "66" // tis upast is unnecessary - the types are identical
#nowarn "58" // possible incorrect indentation..
#nowarn "57" // do not use create_DelegateEvent
#nowarn "51" // address-of operator can occur in the code
open System
open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
exception ReturnException183c26a427ae489c8fd92ec21a0c9a59 of obj
exception ReturnNoneException183c26a427ae489c8fd92ec21a0c9a59

[<assembly: ComVisible (false)>]

[<assembly: CLSCompliant (false)>]

[<assembly: Guid ("020697d7-24a3-4ce4-a326-d2c7c204ffde")>]

[<assembly: AssemblyTitle ("Frack is a F# based socket implementation for high-speed, high-throughput applications.")>]

[<assembly: AssemblyDescription ("Frack is an F# based socket implementation for high-speed, high-throughput applications. It is built on top of SocketAsyncEventArgs, which minimises the memory fragmentation common in the IAsyncResult pattern.")>]

[<assembly: AssemblyProduct ("Frack is a F# based socket implementation for high-speed, high-throughput applications.")>]

[<assembly: AssemblyVersion ("0.1.120726")>]

[<assembly: AssemblyFileVersion ("0.1.120726")>]

[<assembly: AssemblyDelaySign (false)>]

()
