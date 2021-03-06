﻿using Kentor.AuthServices.Configuration;
using Kentor.AuthServices.Saml2P;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace Kentor.AuthServices.WebSso
{
    class Saml2PostBinding : Saml2Binding
    {
        protected internal override bool CanUnbind(HttpRequestData request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.HttpMethod == "POST"
                && (request.Form.Keys.Contains("SAMLResponse")
                    || request.Form.Keys.Contains("SAMLRequest"));
        }

        public override UnbindResult Unbind(HttpRequestData request, IOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var xmlDoc = new XmlDocument()
            {
                PreserveWhitespace = true
            };

            string encodedMessage;
            if (!request.Form.TryGetValue("SAMLResponse", out encodedMessage))
            {
                encodedMessage = request.Form["SAMLRequest"];
            }

            xmlDoc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)));

            string relayState = null;
            request.Form.TryGetValue("RelayState", out relayState);

            return new UnbindResult(xmlDoc.DocumentElement, relayState, TrustLevel.None);
        }

        public override CommandResult Bind(ISaml2Message message)
        {
            if(message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var xml = message.ToXml();
            if(message.SigningCertificate != null)
            {
                var xmlDoc = new XmlDocument()
                {
                    PreserveWhitespace = true
                };

                xmlDoc.LoadXml(xml);

                if (xmlDoc.FirstChild.LastChild.Name.Equals("saml2:Assertion"))
                {
                    ((XmlElement) xmlDoc.FirstChild.LastChild).Sign(message.SigningCertificate, false, message.SigningAlgorithm);
                }
                else
                {
                    xmlDoc.Sign(message.SigningCertificate, true, message.SigningAlgorithm);
                }

                xml = xmlDoc.OuterXml;
            }

            var encodedXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

            var relayStateHtml = string.IsNullOrEmpty(message.RelayState) ? null 
                : string.Format(CultureInfo.InvariantCulture, PostHtmlRelayStateFormatString, message.RelayState);

            var cr = new CommandResult()
            {
                ContentType = "text/html",
                Content = String.Format(
                    CultureInfo.InvariantCulture, 
                    PostHtmlFormatString, 
                    message.DestinationUrl, 
                    relayStateHtml, 
                    message.MessageName, 
                    encodedXml)
            };

            return cr;
        }

        private const string PostHtmlRelayStateFormatString = @"
<input type=""hidden"" name=""RelayState"" value=""{0}""/>";

        private const string PostHtmlFormatString = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN""
""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"">
<body onload=""document.forms[0].submit()"">
<noscript>
<p>
<strong>Note:</strong> Since your browser does not support JavaScript, 
you must press the Continue button once to proceed.
</p>
</noscript>
<form action=""{0}"" method=""post"">
<div>{1}
<input type=""hidden"" name=""{2}""
value=""{3}""/>
</div>
<noscript>
<div>
<input type=""submit"" value=""Continue""/>
</div>
</noscript>
</form>
</body>
</html>";
    }
}
