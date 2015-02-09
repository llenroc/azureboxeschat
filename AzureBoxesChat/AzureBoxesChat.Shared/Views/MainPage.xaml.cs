using AzureBoxesChat.Common;
using AzureBoxesChat.Model;
using Microsoft.Live;
using Microsoft.WindowsAzure.Messaging;
using Microsoft.WindowsAzure.MobileServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechSynthesis;
using Windows.Networking.PushNotifications;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace AzureBoxesChat.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MobileServiceUser user;
        private LiveConnectSession session;
        private string userfirstname;
        private string userlastname;
        private bool isLoggedin = false;

        public PushNotificationChannel PushChannel;

        private MobileServiceCollection<Chat, Chat> items;
        private IMobileServiceTable<Chat> chatTable = App.MobileService.GetTable<Chat>();

        public MainPage()
        {
            this.InitializeComponent();

            if (!CheckForInternetAccess())
            {
                string msg1 = "Se requiere una conexión a Internet para esta aplicación y parece que no estas conectado." + Environment.NewLine + Environment.NewLine;
                string msg2 = "Asegúrate de que tienes una conexión a Internet activa y vuelve a intentarlo.";
                UpdateStatus("No estas conectado a Internet", true);

                new MessageDialog(msg1 + msg2, "No Internet").ShowAsync();
            }
            else
            {
                InitNotificationsAsync();
            }

            // En Windows, el botón Enviar debe hacerse visible siempre y no desde el AppBar
            // no queremos forzar al usuario deslizar hacia arriba cada vez que quiera chatear.
#if WINDOWS_APP
            btnWinSend.Visibility = Windows.UI.Xaml.Visibility.Visible;
            TextInput.Height = btnWinSend.Height;
#endif
        }

        // Check to see if we have Internet access since we need it to talk to Azure
        private bool CheckForInternetAccess()
        {
            // Esta no es la forma más infalible, pero al menos cubre escenarios básicos
            // Como Modo avión
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Si tenemos problemas para conectarse al servicio móvil, tenemos que desactivar algunos controles
        // Esto por si tal vez el usuario va a tratar de conversar de todos modos.
        private async Task SetUIState(bool isEnabled)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TextInput.PlaceholderText = (isEnabled) ? "chatea con otras personas escribiendo aquí" : "debe iniciar sesión en el chat";
                TextInput.IsEnabled = isEnabled;
                btnWinSend.IsEnabled = isEnabled;
                ListItems.Focus(FocusState.Programmatic);
            });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            MessageDialog dlg;
            string msg1 = "", msg2 = "";
            Exception exception = null;

            try
            {
                // Este bloque de código es para recordate que debe crear tus servicio móvil antes de
                // ejecutar la aplicación. Ver detalles y vínculos en la clase Configuracion.cs
                // Puedes eliminar o comentar esta validación una vez que tus servicios de Azure están listos.
                if (!Configuracion.ISAZURECONFIGDONE)
                {
                    UpdateStatus("No se encontró un servicio en la nube", true);
                    msg1 = "Debes preparar tus servicios Azure antes de ejecutar esta aplicación.";
                    dlg = new Windows.UI.Popups.MessageDialog(msg1, "Falta Cloud Services");
                    await dlg.ShowAsync();
                }

                if (CheckForInternetAccess())
                {
                    // Antes de hacer cualquier otra cosa, hay que autenticar al usuario
                    await Authenticate();

                    //Una vez autenticado, vamos a recuperar los últimos artículos de chat de Azure
                    RefreshChatItems();
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                msg1 = "Se ha producido un error al cargar la aplicación." + Environment.NewLine + Environment.NewLine;
                // TO DO: Dividir los distintos errores y ofrecer un adecuado
                // Mensaje de error en msg2 para cada uno.
                msg2 = "Asegúrate de que tienes una conexión a Internet activa y vuelve a intentarlo.";

                await new MessageDialog(msg1 + msg2, "Error de Inicialización").ShowAsync();
                Application.Current.Exit();
            }
        }

        // Autentica al usuario a través de una cuenta de Microsoft (cuenta por defecto en Windows Phone)
        // Autenticación a través de Facebook, Twitter o Google se agregará en una versión futura.
        private async Task Authenticate()
        {
            prgBusy.IsActive = true;
            Exception exception = null;

            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    TextUserName.Text = "Por favor, espera mientras inicias sesión...";
                });

                
                LiveAuthClient liveIdClient = new LiveAuthClient(Configuracion.AzureMobileServicesURI);

                while (session == null)
                {
                    // Inicio de sesión en Microsoft Account
                    LiveLoginResult result = await liveIdClient.LoginAsync(new[] { "wl.basic" });
                    if (result.Status == LiveConnectSessionStatus.Connected)
                    {
                        session = result.Session;
                        LiveConnectClient client = new LiveConnectClient(result.Session);
                        LiveOperationResult meResult = await client.GetAsync("me");
                        user = await App.MobileService
                            .LoginWithMicrosoftAccountAsync(result.Session.AuthenticationToken);

                        userfirstname = meResult.Result["first_name"].ToString();
                        userlastname = meResult.Result["last_name"].ToString();

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            var message = string.Format("Autenticado como: {0} {1}", userfirstname, userlastname);
                            TextUserName.Text = message;
                        });

                        isLoggedin = true;
                        SetUIState(true);
                    }
                    else
                    {
                        session = null;
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateStatus("Debes de iniciar sesión antes de que puedas usar esta aplicación.", true);
                            var dialog = new MessageDialog("Debes iniciar sesión.", "Inicio de sesión requerido.");
                            dialog.Commands.Add(new UICommand("OK"));
                            dialog.ShowAsync();
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                UpdateStatus("Algo salió mal al tratar de iniciar sesión.", true);
                string msg1 = "Se ha producido un error al intentar iniciar sesión." + Environment.NewLine + Environment.NewLine;

                // TO DO: Dividir los distintos errores y ofrecer un adecuado
                // Mensaje de error en msg2 para cada uno.
                string msg2 = "Asegúrate de que tienes una conexión a Internet activa y vuelve a intentarlo.";

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    new MessageDialog(msg1 + msg2, "Error de Autenticación").ShowAsync();
                });
            }
            prgBusy.IsActive = false;
        }


        // Inserta un nuevo mensaje a la conversación mediante la publicación en el servicio móvil
        private async void InsertChatItem(Chat chatItem)
        {
            // Aquí se inserta un nuevo mensaje en la base de datos. Cuando se completa la operación
            // y el servicio móvil lo ha asignado un ID, el elemento se añade al CollectionView
            await chatTable.InsertAsync(chatItem);
        }

        // Obtiene los últimos mensajes de la conversación desde la nube para mostrarlos en la pantalla
        private async void RefreshChatItems()
        {
            prgBusy.IsActive = true;

            if (isLoggedin)
            {
                MobileServiceInvalidOperationException exception = null;
                try
                {
                    int n = 20;
                    items = await chatTable.OrderByDescending(chatitem => chatitem.TimeStamp).Take(n).ToCollectionAsync();
                    if (items.Count > 0)
                    {
                        if (items.Count < n) { n = items.Count; }

                        for (int i = 0; i < (n - 1); i++)
                        {
                            items.Move(0, n - i - 1);
                        }
                    }
                }
                catch (MobileServiceInvalidOperationException e)
                {
                    exception = e;
                }

                if (exception != null)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        new MessageDialog(exception.Message, "Error cargando los mensajes").ShowAsync();
                    });
                }
                else
                {
                    ListItems.ItemsSource = items;
                }
            }
            prgBusy.IsActive = false;
        }

        
        // Útil para los escenarios en los que se podrían haber perdido algunos mensaje
        // o el usuario quiere actualizar la pantalla
        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshChatItems();
        }

        
        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            SendChatLine();
        }

       
        private void ButtonSend_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendChatLine();
            }
        }

        // Prepara el mensaje que se enviará a la nube
        private void SendChatLine()
        {
            string msg = TextInput.Text.Trim();
            if (isLoggedin && msg.Length > 0)
            {
                var chatItem = new Chat { Mensaje = msg, NombreUsuario = String.Format("{0} {1}", userfirstname, userlastname), TimeStamp = DateTime.UtcNow };
                InsertChatItem(chatItem);
                TextInput.Text = "";
            }
        }

        private async void InitNotificationsAsync()
        {
            Exception exception = null;

            try
            {
                var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();

                var hub = new NotificationHub(Configuracion.AzureNotificationHubName, Configuracion.AzureNotificationHubCnxString);
                var result = await hub.RegisterNativeAsync(channel.Uri);

                // Muestra el ID de registro para que sepa que fue exisitoso
                if (result.RegistrationId != null)
                {
                    UpdateStatus("El chat está listo.", false);

                    PushChannel = channel;
                    PushChannel.PushNotificationReceived += OnPushNotification;
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                UpdateStatus("No se pudo inicializar los servicios en la nube para recibir mensajes.", true);
                string msg1 = "Se ha producido un error al inicializar notificaciones." + Environment.NewLine + Environment.NewLine;

                 //TO DO: Dividir los distintos errores y ofrecer un adecuado
                 //Mensaje de error en msg2 para cada uno.
                string msg2 = "Asegúrate de que tienes una conexión a Internet activa y vuelve a intentarlo.";

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    new MessageDialog(msg1 + msg2, "Error de Inicialización").ShowAsync();
                });
            }
        }

        private async Task UpdateStatus(string status, bool isError)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatusMsg.Text = status;

                StatusMsg.Foreground = new SolidColorBrush((isError) ? Colors.Red : Colors.Black);
            });
        }

        private async void OnPushNotification(PushNotificationChannel sender, PushNotificationReceivedEventArgs e)
        {
            String notificationContent = String.Empty;

            e.Cancel = true;

            switch (e.NotificationType)
            {
                // Los Badges aún no funcionan, pueden hacer eso en proximas versiones
                case PushNotificationType.Badge:
                    notificationContent = e.BadgeNotification.Content.GetXml();
                    break;

                // Los Tiles aún no funcionan, pueden hacer eso en proximas versiones
                case PushNotificationType.Tile:
                    notificationContent = e.TileNotification.Content.GetXml();
                    break;

                // La versión actual de esta aplicación sólo soporta notificaciones Toast
                case PushNotificationType.Toast:
                    notificationContent = e.ToastNotification.Content.GetXml();
                    XmlDocument toastXml = e.ToastNotification.Content;

                    //Extrae los datos relevantes del chat para la carga de la notificación Toast
                    XmlNodeList toastTextAttributes = toastXml.GetElementsByTagName("text");
                    string username = toastTextAttributes[0].InnerText;
                    string chatline = toastTextAttributes[1].InnerText;
                    string chatdatetime = toastTextAttributes[2].InnerText;

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => 
                    {
                        var chatItem = new Chat { Mensaje = chatline, NombreUsuario = username };
                        items.Add(chatItem);
                    });

                    break;

                // Las notificaciones Raw tampoco se utilizan en esta versión
                case PushNotificationType.Raw:
                    notificationContent = e.RawNotification.Content;
                    break;
            }
        }

        
        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {

        }

       
        private async void ButtonFeedback_Click(object sender, RoutedEventArgs e)
        {
            //Prepara un simple correo electrónico para redirigido al correo electrónico de soporte
            var mailto = new Uri("mailto:?to=" +
                                    Configuracion.DeveloperSupportEmail +
                                    "&subject=Windows Phone App Feedback: &body=Por favor escriba sus comentarios en el formulario de este correo electrónico para enviarlo:");
            await Windows.System.Launcher.LaunchUriAsync(mailto);
        }
    }
}
