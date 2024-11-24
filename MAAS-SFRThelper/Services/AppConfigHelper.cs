using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;

namespace MAAS_SFRThelper.Services
{
    public static class AppConfig //: IAppConfig
    {
        public static IDictionary<string, string> m_appSettings;

        /// <summary>
        /// Constructor for AppConfig with path to config file
        /// </summary>
        /// <param name="executingAssemblyLocation">Path of the executing assembly location</param>
        public static void GetAppConfig(string executingAssemblyLocation)
        {
            if (string.IsNullOrWhiteSpace(executingAssemblyLocation))
            {
                throw new ArgumentNullException();
            }

            var demoPlanCheckerAppConfigPath = Path.Combine(Path.GetDirectoryName(executingAssemblyLocation) ?? string.Empty,
                                                            $"{Path.GetFileName(executingAssemblyLocation)}.config");

            var doc = XDocument.Load(demoPlanCheckerAppConfigPath);
            m_appSettings = doc
                .Descendants("configuration")
                .Descendants("appSettings")
                .Descendants()
                .ToDictionary(
                    xElement => xElement.Attribute("key")?.Value ?? string.Empty,
                    xElement => xElement.Attribute("value")?.Value ?? string.Empty);
        }

        /// <summary>
        /// Reads a config value by name.
        /// </summary>
        /// <param name="name">The name of the appSettings value</param>
        /// <returns>The config value</returns>
        public static string GetValueByKey(string name)
        {
            var appSettingValue = m_appSettings.FirstOrDefault(kvp => kvp.Key == name);
            return appSettingValue.Equals(default(KeyValuePair<string, string>))
                    || appSettingValue.Equals(new KeyValuePair<string, string>(string.Empty, string.Empty))
                ? null
                : appSettingValue.Value;
       
        }
    }
}
