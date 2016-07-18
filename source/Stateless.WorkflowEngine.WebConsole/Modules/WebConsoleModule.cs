using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class WebConsoleModule : NancyModule
    {
        public WebConsoleModule()
        {
        }

        protected void AddScript(string script)
        {
            if (Context.ViewBag.Scripts == null)
            {
                Context.ViewBag.Scripts = new List<string>();
            }
            ((List<string>)Context.ViewBag.Scripts).Add(script);
        }

        //protected object HandleResult(IControllerResult result)
        //{
        //    if (result == null) throw new ArgumentNullException("result");

        //    ViewResult vr = result as ViewResult;
        //    if (vr != null)
        //    {
        //        return this.View[vr.ViewName, vr.Model];
        //    }

        //    RedirectResult rr = result as RedirectResult;
        //    if (rr != null)
        //    {
        //        return this.Response.AsRedirect(rr.Location);
        //    }

        //    LoginAndRedirectResult lrr = result as LoginAndRedirectResult;
        //    if (lrr != null)
        //    {
        //        return this.LoginAndRedirect(lrr.UserId, DateTime.Now.AddDays(1), lrr.Location);
        //    }

        //    LoginResult lr = result as LoginResult;
        //    if (lr != null)
        //    {
        //        if (lr.Success)
        //        {
        //            return this.Login(lr.UserId, DateTime.Now.AddDays(1));
        //        }
        //        return this.Response.AsJson(lr);
        //    }

        //    JsonResult jr = result as JsonResult;
        //    if (jr != null)
        //    {
        //        return new JsonResponse(jr.Model, new DefaultJsonSerializer());
        //    }

        //    TextResult tr = result as TextResult;
        //    if (tr != null)
        //    {
        //        return Response.AsText(tr.Text);
        //    }

        //    FileResult fr = result as FileResult;
        //    if (fr != null)
        //    {
        //        var response = new Response();
        //        response.Headers.Add("Content-Disposition", String.Format("attachment; filename={0}", fr.FileName));
        //        response.ContentType = fr.ContentType;
        //        response.Contents = 
        //            stream => {
        //            using (var writer = new System.IO.BinaryWriter(stream))
        //            {
        //                writer.Write(fr.FileContents);
        //            }
        //        };
        //        return response;
        //    }

        //    NotFoundResult nfr = result as NotFoundResult;
        //    if (nfr != null)
        //    {
        //        return HttpStatusCode.NotFound;
        //    }

        //    ErrorResult er = result as ErrorResult;
        //    if (er != null)
        //    {
        //        return er.HttpStatusCode;
        //    }

        //    throw new NotSupportedException("Results of type " + result.GetType().Name + " not supported");
        //}

    }
}
