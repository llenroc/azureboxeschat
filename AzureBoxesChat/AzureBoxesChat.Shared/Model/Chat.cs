using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzureBoxesChat.Model
{
    class Chat
    {
        public string Id { get; set; }

        [JsonProperty(PropertyName = "mensaje")]
        public string Mensaje { get; set; }

        [JsonProperty(PropertyName = "nombreusuario")]
        public string NombreUsuario { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public DateTime TimeStamp { get; set; }

        // Propiedad de sólo lectura no almacenada en la nube
        [JsonIgnore]
        public string ChatLine
        {
            get { return this.NombreUsuario + " - " + this.Mensaje; }
        }
    }
}
