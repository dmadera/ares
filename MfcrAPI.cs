using System;
using System.IO;
using System.Net;
using System.Xml;

namespace Ares {
    static class MfcrAPI {
        private const string url = "http://adisrws.mfcr.cz/adistc/axis2/services/rozhraniCRPDPH.rozhraniCRPDPHSOAP";
        private const string action = "getStatusNespolehlivyPlatce";

        public static TaxpayerStatus getTaxpayerStatus(string taxno, string testFileName = null) {
            if (taxno == String.Empty) {
                return TaxpayerStatus.notapayer;
            }

            var doc = new XmlDocument();

            if (testFileName != null) {
                doc.Load(testFileName);
            } else {
                string xmlResponse = MfcrAPI.makeRequest(taxno);
                doc.LoadXml(xmlResponse);
            }

            XmlNode root = doc.DocumentElement;

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("soapenv", root.Attributes["xmlns:soapenv"].Value);

            string status = "NENALEZEN";
            var xmlNode = root.SelectSingleNode(".//*[local-name()='statusPlatceDPH']", nsmgr);
            if (xmlNode != null) status = xmlNode.Attributes["nespolehlivyPlatce"].Value;

            Console.WriteLine("MFCR Taxpayer result: " + status);

            switch (status) {
                case "ANO": return TaxpayerStatus.unreliable;
                case "NE": return TaxpayerStatus.reliable;
                default: return TaxpayerStatus.notapayer;
            }
        }

        private static string makeRequest(string taxno) {
            Console.WriteLine("Making request to MFCR: " + url);
            XmlDocument soapEnvelopeXml = createSoapEnvelope(taxno);
            HttpWebRequest webRequest = createWebRequest();
            insertSoapEnvelopeIntoWebRequest(soapEnvelopeXml, webRequest);

            IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);
            asyncResult.AsyncWaitHandle.WaitOne();

            string soapResult;
            using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
            using (StreamReader rd = new StreamReader(webResponse.GetResponseStream())) {
                soapResult = rd.ReadToEnd();
            }
            return soapResult;
        }

        private static HttpWebRequest createWebRequest() {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Headers.Add("SOAPAction", action);
            webRequest.ContentType = "text/xml;charset=\"utf-8\"";
            webRequest.Accept = "text/xml";
            webRequest.Method = "POST";
            return webRequest;
        }

        private static XmlDocument createSoapEnvelope(string taxno) {
            XmlDocument soapEnvelopeDocument = new XmlDocument();
            soapEnvelopeDocument.LoadXml(string.Format(@"
            <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
                <soapenv:Body>
                    <StatusNespolehlivyPlatceRequest xmlns=""http://adis.mfcr.cz/rozhraniCRPDPH/"">
                        <dic>{0}</dic>
                    </StatusNespolehlivyPlatceRequest>
                </soapenv:Body>
            </soapenv:Envelope>", taxno));
            return soapEnvelopeDocument;
        }

        private static void insertSoapEnvelopeIntoWebRequest(XmlDocument soapEnvelopeXml, HttpWebRequest webRequest) {
            using (Stream stream = webRequest.GetRequestStream()) {
                soapEnvelopeXml.Save(stream);
            }
        }
    }

}