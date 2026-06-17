using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Mercurius.Helpers
{
    /// <summary>
    /// Helpers for controllers that want to support both classic full-page form POSTs
    /// AND AJAX form POSTs (see <c>form.js-ajax-form</c> in _Layout.cshtml). The client
    /// advertises its preference with <c>Accept: application/json</c> or
    /// <c>X-Requested-With: XMLHttpRequest</c>; when either is present we return JSON
    /// shaped exactly as the JS handler expects:
    ///
    ///   { ok: true,  redirect: "/Products", message: "Saved." }   — success
    ///   { ok: false, errors: ["PartCode is required", ...] }       — validation/exception
    /// </summary>
    public static class AjaxFormResults
    {
        /// <summary>
        /// True when the current request should receive a JSON response from a form submit
        /// handler. Falls back to false for plain-old HTML form submits and tag-helper posts.
        /// </summary>
        public static bool WantsJson(this HttpRequest request)
        {
            if (request == null) return false;
            var xrw = request.Headers["X-Requested-With"].ToString();
            if (string.Equals(xrw, "XMLHttpRequest", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var accept = request.Headers["Accept"].ToString();
            return !string.IsNullOrEmpty(accept) && accept.Contains("application/json");
        }

        /// <summary>
        /// JSON response signalling success. <paramref name="redirectUrl"/> tells the
        /// browser where to navigate next; the optional message is shown via toastr on
        /// the destination page (stashed in sessionStorage by the JS handler).
        /// </summary>
        public static IActionResult JsonOk(string redirectUrl, string? message = null)
        {
            return new JsonResult(new
            {
                ok = true,
                redirect = redirectUrl,
                message
            });
        }

        /// <summary>
        /// JSON response signalling failure with a flat list of human-readable messages.
        /// </summary>
        public static IActionResult JsonError(IEnumerable<string> errors)
        {
            return new JsonResult(new
            {
                ok = false,
                errors = errors.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray()
            });
        }

        /// <summary>
        /// JSON response signalling failure with a single message. Convenience overload.
        /// </summary>
        public static IActionResult JsonError(string error)
        {
            return JsonError(new[] { error });
        }

        /// <summary>
        /// Flattens a ModelStateDictionary into "<see cref="field"/>: msg" strings, suitable
        /// for the AJAX error toast. ModelOnly errors (empty key) are returned without the
        /// "(form): " prefix to keep the toast readable.
        /// </summary>
        public static IEnumerable<string> ToErrorList(this ModelStateDictionary modelState)
        {
            foreach (var entry in modelState)
            {
                if (entry.Value == null || entry.Value.Errors.Count == 0) continue;
                foreach (var err in entry.Value.Errors)
                {
                    if (string.IsNullOrWhiteSpace(err.ErrorMessage)) continue;
                    if (string.IsNullOrEmpty(entry.Key))
                    {
                        yield return err.ErrorMessage;
                    }
                    else
                    {
                        yield return $"{entry.Key}: {err.ErrorMessage}";
                    }
                }
            }
        }
    }
}
