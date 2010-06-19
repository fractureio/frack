namespace Frack
open System.IO

/// Defines an environment object that is threaded through all handlers.
type Environment =                        // Should this be available through a Reader only?
  { HTTP_METHOD : string
    SCRIPT_NAME : string
    PATH_INFO : string
    QUERY_STRING : string
    CONTENT_TYPE : string
    CONTENT_LENGTH : int
    SERVER_NAME : string
    SERVER_PORT : int
    HEADERS : Map<string,string>
    QueryString : Map<string,string>
    Version : int*int
    UrlScheme : string
    Input : TextReader                    // Should this be available through a Reader only?
    Errors : TextWriter                   // Should this be available through a Writer only?
    Multithread : bool ref
    Multiprocess : bool ref
    RunOnce : bool ref }