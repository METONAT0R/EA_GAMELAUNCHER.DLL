using log4net;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace GameLauncher
{
	public class WebServicesWrapper
	{
		private static readonly ILog mLogger = LogManager.GetLogger(typeof(WebServicesWrapper));

		private string FormatParameters(string[] parameters)
		{
			string text = "";
			if (parameters != null)
			{
				if (parameters.Length >= 2)
				{
					text = "?" + parameters[0] + "=" + parameters[1];
				}
				for (int i = 2; i < parameters.Length; i += 2)
				{
					string text2 = text;
					text = text2 + "&" + parameters[i] + "=" + parameters[i + 1];
				}
			}
			return text;
		}

		public bool ValidateCertificateCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}

		public string DoCall(string url, string command, string[] parameters, string[][] extraHeaders, RequestMethod requestMethod)
		{
			return DoCall(url, command, parameters, extraHeaders, null, requestMethod);
		}

		public string DoCall(string url, string command, string[] parameters, string[][] extraHeaders, string body, RequestMethod requestMethod)
		{
			string text = command + FormatParameters(parameters);
			if (text.EndsWith(".."))
			{
				text += "End";
			}
			if (url.ToLower().StartsWith("https://"))
			{
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
				ServicePointManager.ServerCertificateValidationCallback = ValidateCertificateCallback;
			}
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url + text);
			httpWebRequest.KeepAlive = false;
			httpWebRequest.ProtocolVersion = HttpVersion.Version10;
			httpWebRequest.ContentType = "application/x-www-form-urlencoded";
			httpWebRequest.Proxy = WebRequest.DefaultWebProxy;
			httpWebRequest.AllowAutoRedirect = true;
			httpWebRequest.MaximumAutomaticRedirections = 10;
			httpWebRequest.UserAgent = "Mozilla/3.0 (compatible; My Browser/1.0)";
			if (extraHeaders != null)
			{
				for (int i = 0; i < extraHeaders.Length; i++)
				{
					switch (extraHeaders[i][0])
					{
					case "Accept":
						httpWebRequest.Accept = extraHeaders[i][1];
						break;
					case "Connection":
						httpWebRequest.Connection = extraHeaders[i][1];
						break;
					case "Content-Type":
						httpWebRequest.ContentType = extraHeaders[i][1];
						break;
					case "Content-Length":
						httpWebRequest.ContentLength = long.Parse(extraHeaders[i][1]);
						break;
					case "Expect":
						httpWebRequest.Expect = extraHeaders[i][1];
						break;
					case "If-Modified-Since":
						httpWebRequest.IfModifiedSince = DateTime.Parse(extraHeaders[i][1]);
						break;
					case "Keep-Alive":
						httpWebRequest.KeepAlive = bool.Parse(extraHeaders[i][1]);
						break;
					case "Referer":
						httpWebRequest.Referer = extraHeaders[i][1];
						break;
					case "Transfer-Encoding":
						httpWebRequest.TransferEncoding = extraHeaders[i][1];
						break;
					case "User-Agent":
						httpWebRequest.UserAgent = extraHeaders[i][1];
						break;
					default:
						httpWebRequest.Headers.Add(extraHeaders[i][0], extraHeaders[i][1]);
						break;
					}
				}
			}
			httpWebRequest.Method = requestMethod.ToString();
			if (!string.IsNullOrEmpty(body))
			{
				Stream requestStream = httpWebRequest.GetRequestStream();
				requestStream.Write(new ASCIIEncoding().GetBytes(body), 0, body.Length);
				requestStream.Close();
			}
			HttpWebResponse httpWebResponse = null;
			StreamReader streamReader;
			string xml;
			try
			{
				httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			}
			catch (WebException ex)
			{
				string text2 = ex.Message;
				int errorCode = 0;
				try
				{
					httpWebResponse = (HttpWebResponse)ex.Response;
					streamReader = new StreamReader(httpWebResponse.GetResponseStream());
					xml = streamReader.ReadToEnd();
					httpWebResponse.Close();
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.LoadXml(xml);
					mLogger.DebugFormat("DoCall Inner Exception: {0}", xmlDocument.InnerXml);
					foreach (XmlNode childNode in xmlDocument.ChildNodes[0].ChildNodes)
					{
						if (childNode.Name == "InnerException")
						{
							foreach (XmlNode childNode2 in childNode.ChildNodes)
							{
								if (childNode2.Name == "Message")
								{
									text2 = childNode2.InnerText;
								}
							}
						}
						if (childNode.Name == "Message" && text2 == ex.Message)
						{
							text2 = childNode.InnerText;
						}
						if (childNode.Name == "ErrorCode")
						{
							errorCode = int.Parse(childNode.InnerText);
						}
					}
				}
				catch
				{
					throw new WebServicesWrapperHttpException(text2);
				}
				throw new WebServicesWrapperServerException(errorCode, text2);
			}
			streamReader = new StreamReader(httpWebResponse.GetResponseStream());
			xml = streamReader.ReadToEnd();
			httpWebResponse.Close();
			return xml;
		}
	}
}
