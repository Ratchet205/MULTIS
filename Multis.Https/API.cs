using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Multis.Https
{
    public abstract class API
    {
        protected class ApiRoute
        {
            public string Pattern { get; set; } = "";
            public Regex Regex { get; set; } = null!;
            public List<string> ParameterNames { get; set; } = [];
            public Func<HttpsListenerContext?, Dictionary<string, string>, Task<(string content, string mimetype)>> Handler { get; set; } = null!;
        }

        protected List<ApiRoute> Routes { get; } = [];

        public virtual async Task Invoke(HttpsListenerContext context)
        {
            if (context.Response == null)
                throw new NullReferenceException("Response object not found.");

            string path = context.Request!.Path;
            Console.WriteLine($"Incoming request path: {path}");

            foreach (var route in Routes)
            {
                var match = route.Regex.Match(path);
                if (match.Success)
                {
                    var parameters = route.ParameterNames.ToDictionary(name => name, name => match.Groups[name].Value);
                    var (content, mimetype) = await route.Handler(context, parameters);
                    await context.Response.Send(content, mimetype);
                    return;
                }
            }

            var (notFoundContent, notFoundType) = await NotFound();
            await context.Response.Send(notFoundContent, notFoundType);
        }

        protected API()
        {
            RegisterStaticEndpoints(); // Automatically register methods with attributes
        }

        private void RegisterStaticEndpoints(Type? type = null, string prefix = "")
        {
            string currentPath = "";
            if (type != null)
            {
                var attr = type.GetCustomAttribute<EndPointPathAttribute>();
                if (attr == null) return;
                currentPath = prefix + (attr.LocalPath == "/" ? "/" : attr.LocalPath.TrimEnd('/'));
            }
            else
            {
                type = GetType();
            }

            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                              .Where(m => m.GetCustomAttribute<EndPointPathAttribute>() != null);

            foreach (var method in methods)
            {
                var methodAttr = method.GetCustomAttribute<EndPointPathAttribute>()!;
                methodAttr.EntirePath = currentPath + (methodAttr.LocalPath == "/" ? "/" : methodAttr.LocalPath.TrimEnd('/'));

                var (regex, paramNames) = CompilePattern(methodAttr.EntirePath);

                var parameters = method.GetParameters();
                if (method.ReturnType != typeof(Task<(string content, string mimetype)>))
                    throw new InvalidOperationException($"Method {method.Name} must return Task<(string, string)>");

                Routes.Add(new ApiRoute
                {
                    Pattern = methodAttr.EntirePath,
                    Regex = regex,
                    ParameterNames = paramNames,
                    Handler = (ctx, routeParams) =>
                    {
                        var methodArgs = parameters.Select(p =>
                        {
                            if (p.ParameterType == typeof(HttpsListenerContext))
                                return ctx;
                            if (routeParams.TryGetValue(p.Name!, out var val))
                                return Convert.ChangeType(val, p.ParameterType);
                            return null;
                        }).ToArray();

                        var result = method.Invoke(null, methodArgs);
                        return (Task<(string content, string mimetype)>)result!;
                    }
                });

                if (methodAttr.ShouldLog)
                    Console.WriteLine(methodAttr.GetLogMessage());
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                                       .Where(t => t.GetCustomAttribute<EndPointPathAttribute>() != null))
            {
                RegisterStaticEndpoints(nested, currentPath);
            }
        }

        private static (Regex regex, List<string> paramNames) CompilePattern(string pattern)
        {
            var paramNames = new List<string>();
            var regexPattern = Regex.Replace(pattern, @"\{(\w+)\}", match =>
            {
                paramNames.Add(match.Groups[1].Value);
                return $"(?<{match.Groups[1].Value}>[^/]+)";
            });
            regexPattern = "^" + regexPattern.TrimEnd('/') + "$";
            return (new Regex(regexPattern, RegexOptions.Compiled), paramNames);
        }

        public virtual Task<(string content, string mimetype)> NotFound()
        {
            return Task.FromResult(("{\"404\":\"Not Found\"}", "application/json"));
        }

        // Sample API usage
        private class TestApi : API
        {
            [EndPointPath("/test")]
            public static Task<(string content, string mimetype)> Test()
            {
                return Task.FromResult(("{\"test\":\"Test\"}", "application/json"));
            }

            [EndPointPath("/user/{name}")]
            public static Task<(string content, string mimetype)> GetUser(string name)
            {
                return Task.FromResult(($"{{\"user\":\"{name}\"}}", "application/json"));
            }

            [EndPointPath("v1")]
            private class V1
            {
                [EndPointPath("/hello/{who}")]
                public static Task<(string content, string mimetype)> Hello(HttpsListenerContext? ctx, string who)
                {
                    return Task.FromResult(($"{{\"hello\":\"{who}\"}}", "application/json"));
                }
            }
        }
    }

    public class APIEndPointNotFoundException(string message) : Exception(message) { }
    public class IsNoAPIEndPointException(string message) : Exception(message) { }
}
