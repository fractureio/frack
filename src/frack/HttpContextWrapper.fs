namespace Frack
/// Converts HttpContext, HttpListenerContext, etc. into an HttpContextBase.
module HttpContextWrapper =
  // TODO: Make these extension methods.
  open System
  open System.Net
  open System.Web

  /// Create an HttpContextBase from an HttpContext.
  let createFromHttpContext (context:HttpContext) =
    System.Web.HttpContextWrapper(context)

  /// Create an HttpContextBase from an HttpContextListener.
  let createFromHttpListenerContext (context:HttpListenerContext) =
    { new HttpContextBase() with
        override this.Request =
          { new HttpRequestBase() with
              override this.HttpMethod = context.Request.HttpMethod
              override this.Url = context.Request.Url
              override this.QueryString = context.Request.QueryString
              override this.Headers = context.Request.Headers
              override this.ContentType = context.Request.ContentType
              override this.ContentLength = Convert.ToInt32(context.Request.ContentLength64) 
              override this.InputStream = context.Request.InputStream } }