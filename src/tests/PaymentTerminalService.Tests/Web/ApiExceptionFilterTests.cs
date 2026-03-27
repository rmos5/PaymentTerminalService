using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentTerminalService.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Routing;

namespace PaymentTerminalService.Web.Tests
{
    [TestClass]
    public class ApiExceptionFilterTests
    {
        private static HttpActionExecutedContext CreateContext(Exception exception)
        {
            var config  = new HttpConfiguration();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");
            request.SetConfiguration(config);

            var controllerContext = new HttpControllerContext(config, new HttpRouteData(new HttpRoute()), request);
            var actionContext     = new HttpActionContext { ControllerContext = controllerContext };

            return new HttpActionExecutedContext(actionContext, exception);
        }

        private static ErrorResponse ReadErrorResponse(HttpActionExecutedContext context)
        {
            return context.Response.Content
                .ReadAsAsync<ErrorResponse>()
                .GetAwaiter()
                .GetResult();
        }

        // ─── Status code mapping ─────────────────────────────────────────────────────

        [TestMethod]
        public void OnException_ApiBadRequestException_Returns400()
        {
            var context = CreateContext(new ApiBadRequestException("bad input"));

            new ApiExceptionFilter().OnException(context);

            Assert.AreEqual(HttpStatusCode.BadRequest, context.Response.StatusCode);
        }

        [TestMethod]
        public void OnException_ApiNotFoundException_Returns404()
        {
            var context = CreateContext(new ApiNotFoundException("not found"));

            new ApiExceptionFilter().OnException(context);

            Assert.AreEqual(HttpStatusCode.NotFound, context.Response.StatusCode);
        }

        [TestMethod]
        public void OnException_ApiConflictException_Returns409()
        {
            var context = CreateContext(new ApiConflictException("conflict"));

            new ApiExceptionFilter().OnException(context);

            Assert.AreEqual(HttpStatusCode.Conflict, context.Response.StatusCode);
        }

        [TestMethod]
        public void OnException_UnknownException_Returns500()
        {
            var context = CreateContext(new InvalidOperationException("unexpected"));

            new ApiExceptionFilter().OnException(context);

            Assert.AreEqual(HttpStatusCode.InternalServerError, context.Response.StatusCode);
        }

        // ─── Error response body ──────────────────────────────────────────────────────

        [TestMethod]
        public void OnException_ApiBadRequestException_ResponseCodeIsBadRequest()
        {
            var context = CreateContext(new ApiBadRequestException("bad input"));

            new ApiExceptionFilter().OnException(context);

            var error = ReadErrorResponse(context);
            Assert.AreEqual("BadRequest", error.Code);
        }

        [TestMethod]
        public void OnException_ExceptionMessage_AppearsInResponseMessage()
        {
            var context = CreateContext(new ApiBadRequestException("invalid amount"));

            new ApiExceptionFilter().OnException(context);

            var error = ReadErrorResponse(context);
            Assert.AreEqual("invalid amount", error.Message);
        }

        [TestMethod]
        public void OnException_WithInnerException_AppendedToMessage()
        {
            var inner   = new Exception("inner detail");
            var context = CreateContext(new ApiBadRequestException("outer", inner));

            new ApiExceptionFilter().OnException(context);

            var error = ReadErrorResponse(context);
            StringAssert.Contains(error.Message, "outer");
            StringAssert.Contains(error.Message, "inner detail");
        }

        [TestMethod]
        public void OnException_WithoutInnerException_MessageIsExceptionMessageOnly()
        {
            var context = CreateContext(new ApiNotFoundException("terminal not found"));

            new ApiExceptionFilter().OnException(context);

            var error = ReadErrorResponse(context);
            Assert.AreEqual("terminal not found", error.Message);
        }

        [TestMethod]
        public void OnException_UnknownException_ResponseCodeIsInternalServerError()
        {
            var context = CreateContext(new Exception("boom"));

            new ApiExceptionFilter().OnException(context);

            var error = ReadErrorResponse(context);
            Assert.AreEqual("InternalServerError", error.Code);
        }
    }
}