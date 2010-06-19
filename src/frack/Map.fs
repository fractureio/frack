namespace Frack
module Map =
  open System.Collections.Specialized

  let fromNameValueCollection (col:NameValueCollection) =
    let folder (h:Map<string,string>) (key:string) =
      Map.add key col.[key] h 
    col.AllKeys |> Array.fold (folder) Map.empty