using System;
using System.Collections.Generic;
using System.Text;

namespace AzureBoxesChat.Common
{
    public static class Configuracion
    {
        //TODO: Una vez que haz configurado las credenciales Azure requeridos, debes cambiar
        // esta constante a true
        public const bool ISAZURECONFIGDONE = false;

        // Credenciales de Azure Mobile Services
        public const string AzureMobileServicesURI = "https://Tu-servicio-movil";
        public const string AzureMobileServicesAppKey = "Pegar aquí el token de tu servicio móvil";

        // Credenciales de Azure Notification Hub
        public const string AzureNotificationHubName = "Pegar el nombre de tu servicio hub";
        public const string AzureNotificationHubCnxString = "Pegar la cadena de conexión compartida de tu servicio hub";

        // Correo de contacto del desarrollador para sporte
        public const string DeveloperSupportEmail = "support@tudominio.com";
    }
}
