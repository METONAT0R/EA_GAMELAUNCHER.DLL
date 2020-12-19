using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace GameLauncher
{
	public class Engine : WebServicesWrapper
	{
		private static readonly Engine _instance = new Engine();

		public static Engine Instance => _instance;

		private Engine()
		{
		}

		public Dictionary<string, string> Login(string serverUrl, string email, string password, string region)
		{
			StringBuilder stringBuilder = new StringBuilder();
			XmlWriter xmlWriter = new XmlTextWriter(new StringWriter(stringBuilder));
			xmlWriter.WriteStartElement("Credentials", "http://schemas.datacontract.org/2004/07/Victory.DataLayer.Serialization");
			xmlWriter.WriteStartElement("Email");
			xmlWriter.WriteString(email);
			xmlWriter.WriteEndElement();
			xmlWriter.WriteStartElement("Password");
			xmlWriter.WriteString(password);
			xmlWriter.WriteEndElement();
			xmlWriter.WriteStartElement("Region");
			xmlWriter.WriteString(region);
			xmlWriter.WriteEndElement();
			xmlWriter.WriteEndElement();
			xmlWriter.Flush();
			xmlWriter.Close();
			return Login(serverUrl, "/User/AuthenticateUser2", stringBuilder);
		}

		public Dictionary<string, string> SSOLogin(string serverUrl, string token, string region)
		{
			return SSOLogin(serverUrl, token, region, eualaAccepted: false);
		}

		public Dictionary<string, string> SSOLogin(string serverUrl, string token, string region, bool eualaAccepted)
		{
			StringBuilder stringBuilder = new StringBuilder();
			XmlWriter xmlWriter = new XmlTextWriter(new StringWriter(stringBuilder));
			xmlWriter.WriteStartElement("Token", "http://schemas.datacontract.org/2004/07/Victory.DataLayer.Serialization");
			xmlWriter.WriteStartElement("EualaAccepted");
			xmlWriter.WriteString(eualaAccepted ? "true" : "false");
			xmlWriter.WriteEndElement();
			xmlWriter.WriteStartElement("Region");
			xmlWriter.WriteString(region);
			xmlWriter.WriteEndElement();
			xmlWriter.WriteStartElement("Value");
			xmlWriter.WriteString(token);
			xmlWriter.WriteEndElement();
			xmlWriter.WriteEndElement();
			xmlWriter.Flush();
			xmlWriter.Close();
			return Login(serverUrl, "/User/AuthenticateUserByToken", stringBuilder);
		}

		private Dictionary<string, string> Login(string url, string command, StringBuilder body)
		{
			string[][] extraHeaders = new string[2][]
			{
				new string[2]
				{
					"Content-Type",
					"text/xml;charset=utf-8"
				},
				new string[2]
				{
					"Content-Length",
					body.Length.ToString(CultureInfo.InvariantCulture)
				}
			};
			string xml = DoCall(url, command, null, extraHeaders, body.ToString(), RequestMethod.POST);
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.LoadXml(xml);
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			_ = string.Empty;
			_ = string.Empty;
			_ = string.Empty;
			foreach (XmlNode childNode in xmlDocument.FirstChild.ChildNodes)
			{
				if (childNode.Name == "securityToken" || childNode.Name == "userId" || childNode.Name == "remoteUserId" || childNode.Name == "username")
				{
					dictionary.Add(childNode.Name, childNode.InnerText);
				}
			}
			return dictionary;
		}

		public void SetRegion(string serverUrl, string userId, string securityToken, int regionId)
		{
			string command = $"/User/SetRegion?userId={userId}&regionId={regionId}";
			string[][] extraHeaders = new string[4][]
			{
				new string[2]
				{
					"Content-Type",
					"text/xml;charset=utf-8"
				},
				new string[2]
				{
					"Content-Length",
					"0"
				},
				new string[2]
				{
					"userId",
					userId
				},
				new string[2]
				{
					"securityToken",
					securityToken
				}
			};
			DoCall(serverUrl, command, null, extraHeaders, RequestMethod.POST);
		}
	}
}
