namespace Frack
type Response =
  { Status  : int
    Headers : Map<string,string>
    Body    : seq<string> }