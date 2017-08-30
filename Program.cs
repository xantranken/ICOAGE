using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using HtmlAgilityPack;

namespace ICOAGE
{
    class Program
    {
        static CookieContainer cc;

        static string projectId;

        static string postData = "";

        static List<string> usernames = new List<string>();
        static List<string> passwords = new List<string>();
        static List<CookieContainer> cookies = new List<CookieContainer>();

        static List<string> inputs;
        static List<string> values;

        static void Main(string[] args)
        {

            Console.WriteLine("Please set project ID:");
            projectId = Console.ReadLine();
            //projectId = "171";
            Console.WriteLine("Loading project information...");
            HttpClient hc;
            HtmlDocument doc;
            LoadEntry:
            try
            {
                hc = new HttpClient() { Timeout = new TimeSpan(0, 0, 20) };
                HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, string.Format("https://www.icoage.com/?p=Lang&lang=en&p2=D&id={0}", projectId));
                AddDefaultHeaders(ref hrm);
                string response = hc.SendAsync(hrm).Result.Content.ReadAsStringAsync().Result;
                doc = new HtmlDocument();
                doc.LoadHtml(response);
                bool loaded = false;
                foreach (var h in doc.DocumentNode.Descendants("h1"))
                {
                    Console.WriteLine(string.Format("Project name: {0}", h.InnerHtml));
                    loaded = true;
                    break;
                }
                if (!loaded) goto LoadEntry;
            }
            catch
            {
                Console.WriteLine("Failed. Retrying...");
                goto LoadEntry;
            }

            while (true)
            {
                Console.WriteLine("-------------------------------------");
                Console.WriteLine(string.Format("{0} user{1} currently recorded.", usernames.Count, usernames.Count > 1 ? "s" : ""));
                for (int i = 0; i < usernames.Count; i++)
                    Console.WriteLine(string.Format("\t{0}", usernames[i]));
                Console.WriteLine("-------------------------------------");
                Console.WriteLine("Please choose an operation:");
                Console.WriteLine("\tadd: add a new user");
                Console.WriteLine("\trefresh: keep user sessions from expiring");
                Console.WriteLine("\tstart: start the purchase");
                Console.Write("Choose action:");
                string op = Console.ReadLine().ToLower();
                if (op == "add")
                {
                    Console.WriteLine("Please input username:");
                    string un = Console.ReadLine();
                    Console.WriteLine("Please input password");
                    string pw = Console.ReadLine();
                    Console.WriteLine("Verifying username and password...");
                    cc = new CookieContainer();
                    hc = new HttpClient(new HttpClientHandler() { CookieContainer = cc }) { Timeout = new TimeSpan(0, 0, 20) };

                    HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, "https://www.icoage.com/?p=Lang&lang=en&p2=login");
                    AddDefaultHeaders(ref hrm);
                    var res = hc.SendAsync(hrm).Result;
                    
                    hrm = new HttpRequestMessage(HttpMethod.Post, "https://www.icoage.com/?p=login&do=login");
                    AddDefaultHeaders(ref hrm);
                    hrm.Content = new StringContent(string.Format("mobile={0}&password={1}", WebUtility.UrlEncode(un), WebUtility.UrlEncode(pw)), System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                    hrm.Headers.TryAddWithoutValidation("Referer", "https://www.icoage.com/?p=login");
                    var a = hc.SendAsync(hrm).Result.Content.ReadAsStringAsync().Result;
                    if (a.Contains("My Identity Information"))
                    {
                        usernames.Add(un);
                        passwords.Add(pw);
                        CookieContainer c = new CookieContainer();
                        foreach (Cookie ccc in cc.GetCookies(new Uri("https://www.icoage.com")))
                            c.Add(ccc);
                        cookies.Add(c);
                        Console.WriteLine("Success.");
                    }
                    else
                    {
                        Console.WriteLine("Username and password cannot be verified.");
                    }
                }
                else if(op == "refresh")
                {
                    for ( int i = 0; i < usernames.Count; i ++)
                    {
                        try
                        {
                            Console.WriteLine(string.Format("Refreshing {0}...", usernames[i]));
                            hc = new HttpClient(new HttpClientHandler() { CookieContainer = cookies[i] }) { Timeout = new TimeSpan(0, 0, 20) };
                            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, "https://www.icoage.com/?p=mMain");
                            AddDefaultHeaders(ref hrm);
                            var a = hc.SendAsync(hrm).Result.Content.ReadAsStringAsync().Result;
                            if (a.Contains("My Identity Information"))
                            {
                                Console.WriteLine("OK");
                            }
                            else
                            {
                                Console.WriteLine("Cannot refresh");
                            }
                        }
                        catch
                        {
                            Console.WriteLine("An error occured");
                        }
                    }
                }
                else if(op == "start")
                {
                    if (usernames.Count == 0)
                    {
                        Console.WriteLine("Please add users first");
                    }
                    else
                    {
                        Console.WriteLine("Obtaining project infomation...");
                        Entry1:
                        try
                        {
                            Task[] loads = new Task[5];
                            for (int i = 0; i < loads.Length; i++)
                            {
                                loads[i] = LoadDetails();
                            }
                            Task.WaitAll(loads);

                            Task[] tasks = new Task[usernames.Count*4];
                            for (int i = 0; i < tasks.Length; i ++)
                            {
                                tasks[i] = Working((int)(i/4.0));
                            }
                            Task.WaitAll(tasks);
                        }
                        catch
                        {
                            Console.WriteLine("Error. Retrying...");
                            goto Entry1;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Unknown action");
                }
            }
        }

        private static async Task LoadDetails()
        {
            Entry1:
            while (true)
            {
                try
                {
                    HttpClient  hc = new HttpClient(new HttpClientHandler() { CookieContainer = cookies[0], AllowAutoRedirect = false }) { Timeout = new TimeSpan(0, 0, 20) };
                    HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, string.Format("https://www.icoage.com/?p=invest&id={0}", projectId));
                    AddDefaultHeaders(ref hrm);
                    var res = await hc.SendAsync(hrm);
                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("Project not yet started");
                        goto Entry1;
                    }
                    else
                    {
                        var r = await res.Content.ReadAsStringAsync();
                        if (r.Contains("Fill in information"))
                        {
                            HtmlDocument doc = new HtmlDocument();
                            doc.LoadHtml(r);
                            inputs = new List<string>();
                            values = new List<string>();

                            foreach (var i in doc.DocumentNode.Descendants("input"))
                                if (i.Attributes["name"] != null)
                                {
                                    inputs.Add(i.Attributes["name"].Value);
                                    if (i.Attributes["value"] == null)
                                        values.Add("");
                                    else
                                        values.Add(i.Attributes["value"].Value);
                                }
                            foreach (var i in doc.DocumentNode.Descendants("select"))
                                if (i.Attributes["name"] != null)
                                {
                                    inputs.Add(i.Attributes["name"].Value);
                                    foreach (var o in i.Descendants("option"))
                                    {
                                        if (o.Attributes["value"] == null)
                                            values.Add("");
                                        else
                                            values.Add(o.Attributes["value"].Value);
                                    }
                                }

                            if (inputs.Count == 0)
                            {
                                Console.WriteLine("Fatal error. Cannot load project info");
                                goto Entry1;
                            }
                            else
                            {
                                if (postData != "")
                                {
                                    await Task.Delay(1000);
                                    return;
                                }
                                for (int i = 0; i < inputs.Count; i++)
                                {
                                    if (inputs[i].ToLower().Contains("number"))
                                        values[i] = "1";
                                    inputs[i] = WebUtility.HtmlDecode(inputs[i]);
                                    values[i] = WebUtility.HtmlDecode(values[i]);

                                    postData += WebUtility.UrlEncode(inputs[i]) + "=" + WebUtility.UrlEncode(values[i]) + (i == inputs.Count - 1 ? "" : "&");
                                }
                                Console.WriteLine("Project info loaded. Investment started");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Cannot load page. Retrying...");
                            goto Entry1;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Error loading info page. Retrying...");
                    goto Entry1;
                }
            }
        }

        private static async Task Working(int i)
        {
            while (true)
            {
                try
                {
                    HttpClient hc = new HttpClient(new HttpClientHandler() { CookieContainer = cookies[i] }) { Timeout = new TimeSpan(0, 0, 20) };
                    HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Post, "https://www.icoage.com/?p=invest&do=invest");
                    AddDefaultHeaders(ref hrm);
                    hrm.Content = new StringContent(postData, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                    hrm.Headers.TryAddWithoutValidation("Referer", string.Format("https://www.icoage.com/?p=invest&id={0}", projectId));
                    string res = await (await hc.SendAsync(hrm)).Content.ReadAsStringAsync();
                    if (res.Contains("balance is insufficient"))
                    {
                        Console.WriteLine(string.Format("#{0} [{1}] Balance insufficient", i, usernames[i]));
                    }
                    else
                    {
                        Console.WriteLine(string.Format("#{0} [{1}] Request sent", i, usernames[i]));
                    }
                }
                catch
                {
                    Console.WriteLine(string.Format("#{0} [{1}] Error while sending the reuqest", i, usernames[i]));
                }
            }
        }

        private static void AddDefaultHeaders(ref HttpRequestMessage hrm)
        {
            hrm.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            hrm.Headers.TryAddWithoutValidation("Accept-Encoding", "deflate, br");
            hrm.Headers.TryAddWithoutValidation("Accept-Language", "zh-TW,zh;q=0.8,en-US;q=0.6,en;q=0.4");
            hrm.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.101 Safari/537.36");
            hrm.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        }
    }
}
