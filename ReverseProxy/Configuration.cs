using System;
using System.Configuration;
using System.Collections.Generic;

namespace ReverseProxy
{
    public class ProxyConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty ("translations", IsDefaultCollection=false)]
        [ConfigurationCollection(
            typeof(ProxyTranslations), 
            AddItemName="add", 
            RemoveItemName="remove", 
            ClearItemsName="clear")]
        public ProxyTranslations translations { get {return (ProxyTranslations)base["translations"];} }
    }

    public class ProxyTranslations : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ProxyTranslation();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ProxyTranslation)element).request;
        }

        public void Add(ProxyTranslation pTranslation)
        {
            BaseAdd(pTranslation);
        }
    }

    public class ProxyTranslation : ConfigurationSection
    {
        [ConfigurationProperty ("request", IsKey=true, IsRequired=true)]
        public string request { get { return (string)base["request"]; } }
        [ConfigurationProperty("destination")]
        public string destination { get { return (string)base["destination"]; } }
        [ConfigurationProperty("headers")]
        public string headers_string { get { return (string)base["headers"]; } }
        public string[] headers { get { return headers_string.Split(','); } }
    }


}