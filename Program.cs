/*------------------------------------------------------------------------------
 * Claire Durand
 * Pochet du Courval
 * 
 * Projet de numérisation des panoplies
 * 
 * Juin/Juillet/Août 2014
 * 
 * 
 * Ce programme est destiné à intéragir avec l'application html/js/jQuery jointe pour afficher une animation qui présente un semblant de photo 3D.
 * Le projet consiste de photos de tout les angles du flacon, organisée de façon incrémentée dans le fichier html pour permettre un trompe-l'oeil interactif.
 * L'appli Kinect place le résultat de la caméra dans la même page html que l'animation et traque le squelette de l'utilisateur primaire,
 * en particulier sa main droite, afin que l'utilisateur puisse se voir utiliser sa main droite comme souris d'ordinateur virtuelle.
 * 
 * UTILISATION:
 * L'appli se sert de la profondeur de la main par rapport au visage de l'utilisateur comme évènement de "click." 
 * Si la main de l'utilisateur se trouve sur une plane à moins de 25 cm devant le visage de l'utilisateur faisant face au capteur Kinect, 
 * le bouton de la souris proverbiale est levée. 
 * Si l'utilisateur enfonce sa main passé ces 25 cm, l'animation agira comme si l'utilisateur avait enfoncé le bouton de sa souris, et 
 * il ou elle pourra "clicker et tirer" l'image (click and drag) dans toutes les directions.
 * De plus, l'utilisateur a l'option de choisir la main dominante à utiliser ou s'il ou elle souhaite un mouvement fluide opposé au corps.
 * Le TrackMode établit laquelle de ces options est implémentée. Si l'un des boutons est cliqué, le programme recoit un message qui change 
 * le contenu de TrackMode. Si TrackMode est une des mains, le logiciel traquera le joint approprié comme expiqué ci-dessus. Sinon, si 
 * le corps est sélectionné, l'utilisateur doit commencer à plus de 1.75m du capteur, puis s'avancer dans la zone pour "cliquer" et prendre
 * le contrôle de l'animation qui se déplacera opposée au mouvements de l'utilisateur grâce à des transformations placées dans 
 * SensorSkeletonFrameReady.
 *
 * 
 *------------------------------------------------------------------------------
*/


#pragma warning disable 0649    // get past MouseData not being initialized warning...it needs to be there for p/invoke

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Fleck;
using Microsoft.Kinect;
using Kinect.Toolbox;
using KinectCursorController;

namespace Kinect.Server
{
    class Program
    {
        // Establishes the websocket relationship variable for the client(s).
        //// Etablit la variable de relation websocket du client.
        static List<IWebSocketConnection> _sockets = new List<IWebSocketConnection>();

        // Array in which can be stored the (up to 6, 7?) skeletons to be displayed.
        //// Tableau de données dans lequel on peut placer 7 squelettes à imprimer sur l'écran.
        static Skeleton[] _skeletons = new Skeleton[6];

        // Parameter variable
        //// Variable de paramètre.
        static Mode _mode = Mode.Color;

        string TrackMode = "Right"; 

        // Establishes the relative maximum height and width of the skeleton frame.
        //// Etablit la hauteur et largeur maximale relative de la trame d'image du squelette.
        float SkeletonMaxX = 0.60f;
        float SkeletonMaxY = 0.40f;


        // Main function
        //// Fonction principale
        static void Main(string[] args)
        {
            // Creates a variable in order to be able to call the non static method "InitializeKinect."
            //// Créé une nouvelle variable pour permettre d'appeler la méthode non-statique "InitializeKinect."
            Program p = new Program();

            // Establishes the variable for the websocket server and fills it with the string for the communication port.
            //// Etalit la variable pour le serveur websocket et la remplit avec la chaine pour le port de communication. 
            var server = new WebSocketServer("ws://0.0.0.0:8181");

            // Starts the server.
            //// Démarre le serveur.
            server.Start(socket =>
            {
                // Handler for the socket's open.
                //// Gestionnaire d'évènement pour l'ouverture du websocket. 
                socket.OnOpen = () =>
                {
                    _sockets.Add(socket);
                };

                // Handler for the the socket's close.
                //// Gestionnaire d'évenement pour la fermeture du websocket.
                socket.OnClose = () =>
                {
                    _sockets.Remove(socket);
                };

                // Handler for the socket's reception of messages.
                //// Gestionnaire pour la réception des messages.
                socket.OnMessage = message =>
                {
                    // C'est ici qu'on va pouvoir placer le changement associé avec une variable TrackMode qui determine 
                    // si l'utilisateur contrôle l'animation avec sa main gauche, droite, ou son corps entier...

                    switch (message)
                    {
                        case "Right":
                            p.TrackMode = "Right";
                            break;
                        case "Left":
                            p.TrackMode = "Left";
                            break;
                        case "Body":
                            p.TrackMode = "Body";
                            break;
                        default:
                            break;
                    }

                    Console.WriteLine("Switched to " + message);
                };
            });

            // Call the placeholder "p" class method to initialize the Kinect and start the sensor streams.
            //// Appelle le placeholder "p" pour permettre d'initializer la Kinect et démarrer les flux de données du capteur.
            p.InitializeKinect();

            // Ouvre le fichier html de l'application sauvegardé au chemin suivant avec google chrome
            Process.Start("iexplore.exe", @"W:\Service_Innovation\Stagiaires\Claire\KinectHTML5IE\Kinect.Client\Animation.html");

            // Read messages from the Console.
            //// Interprète les message de la Console.
            Console.ReadLine();
        }

        /// <summary>
        /// Method to start the Kinect and its color, depth, and skeleton streams with the desired parameters.
        /// // Méthode pour démarrer la Kinect et ses flux de données de couleur, de profondeur, et squelette avec les paramètres désirés.
        /// </summary>
        private void InitializeKinect()
        {
            // Chooses a sensor.
            //// Choisit une capteur.
            var sensor = KinectSensor.KinectSensors.SingleOrDefault();

            // Sets and applies the transform parameters to smooth the skeleton data.
            //// Définit et applique les paramètres de transformation pour "lisser" les données de squelette.
            var parameters = new TransformSmoothParameters();
            parameters.Smoothing = 0.5f;
            parameters.Correction = 0.1f;
            parameters.Prediction = 0.2f;
            parameters.JitterRadius = 0.05f;
            parameters.MaxDeviationRadius = 0.1f;

            // Enable and start the sensor streams if available.
            //// Permet et demarre les flux de données du capteur, si disponible.
            if (sensor != null)
            {
                // Enable the color and depth stream at the specified formats, and the skeleton stream using the set parameters.
                //// Permet les flux de couleur et de profondeur aux formats spécifiés, et le flux de données squelette en utilisant les paramètres spécifiés. 
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30); 
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                sensor.SkeletonStream.Enable(parameters);

                // Iterates each new frame.
                //// Itère chaque trame d'image.
                sensor.AllFramesReady += Sensor_AllFramesReady;

                // Try starting the playback
                //// Démarre le playback.
                try
                {
                    sensor.Start();
                }
                catch (System.IO.IOException)
                {
                    // another app is using Kinect.
                    //// une autre application utilise déjà la Kinect.
                    Console.Write("Another app is using Kinect");
                }
            }
        }

        
        /// <summary>
        /// Event handler to implement any time all frames are ready.
        /// // Gestionnaire d'évènement à implémenter dès que toutes les trames sont prêtes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // Place la trame d'image couleur dans une variable plus facile d'accès.
            using (var frame = e.OpenColorImageFrame())
            {
                // Vérifier que la trame n'est pas vide.
                if (frame != null)
                {
                    // Serialize la trame et place le résultat dans la variable "blob."
                    var blob = frame.Serialize();

                    // Selon le mode séléctioné chez le client...
                    if (_mode == Mode.Color)
                    {
                        // Envoie le résultat à chaque websocket client.
                        foreach (var socket in _sockets)
                        {
                            socket.Send(blob);
                        }
                    }
                }
            }
            
            // Place la trame d'image profondeur dans une variable plus facile d'accès.
            using (var frame = e.OpenDepthImageFrame())
            {
                // Vérifie que la trame n'est pas vide.
                if (frame != null)
                {
                    // Sérialize la trame dans une variable "blob."
                    var blob = frame.Serialize();

                    // Selon le mode séléctioné chez le client (celui-ci n'existe plus.)
                    if (_mode == Mode.Depth)
                    {
                        // Envoie le résultat à chaque websocket client.
                        foreach (var socket in _sockets)
                        {
                            socket.Send(blob);
                        }
                    }
                }
            }

            // Place la trame d'image squelette dans une variable plus facile d'accès.
            using (var frame = e.OpenSkeletonFrame())
            {
                // Vérifie que la trame n'est pas vide.
                if (frame != null)
                {
                    // Place le contenu de la trame dans un tableau de données qui accumule plusieurs squelettes d'utilisateurs.
                    frame.CopySkeletonDataTo(_skeletons);
                    var users = _skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked).ToList();

                    // Vérifie qu'il y  au moins un squelette d'utilisateur.
                    if (users.Count > 0)
                    {
                        // Sérialize les données de squelette des utilisateurs.
                        string json = users.Serialize();

                        // Envoie le résultat à tout les clients websockets
                        foreach (var socket in _sockets)
                        {
                            socket.Send(json);
                        }
                    }

                    // Appelle la fonction de gestion d'évènement des données de squelette.
                    SensorSkeletonFrameReady(e);
                }
            }
        }

        /// <summary>
        /// Gestion d'évènement des données de squelette.
        /// </summary>
        /// <param name="e"></param>
        void SensorSkeletonFrameReady(AllFramesReadyEventArgs e) 
        {
            // Réouverture des données de squelette.
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                // Se débarasse de la trame si elle est vide.
                if (skeletonFrameData == null)
                {
                    return;
                }

                var allSkeletons = new Skeleton[skeletonFrameData.SkeletonArrayLength];

                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                // S'occupe de chaque squelette dans le tableau.
                foreach (Skeleton sd in allSkeletons)
                {
                    if (TrackMode == "Left")
                    {
                        //// Le premier squelette détecté a le contrôle de la souris.
                        if (sd.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            //// Suit les mouvements de la main droite.
                            if (sd.Joints[JointType.HandLeft].TrackingState == JointTrackingState.Tracked)
                            {
                                //// Créé une variable locale plus accessible pour le poignet droit du squelette principal.
                                //// Echelonne les mouvements du poignet pour correspondre à la taille de l'écran.
                                var handLeft = sd.Joints[JointType.HandLeft];
                                var scaledLeftHand = handLeft.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                                // Créé une variable locale plus accessible pour la tête du squelette principal.
                                // Echelonne les mouvements de la tête pour correspondre à la taille de l'écran.
                                var head = sd.Joints[JointType.Head];
                                var scaledHead = head.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                                //// Créé des variables plus accessibles pour la position échelonnée de la main droite.
                                var cursorX = ((int)scaledLeftHand.Position.X);
                                var cursorY = ((int)scaledLeftHand.Position.Y);

                                //// Appel à la fonction qui vérifie si l'utilisateur essaye de détendre le bouton de la souris.
                                var leftClick = CheckForClickHold(scaledLeftHand, scaledHead);

                                NativeMethods.SendMouseInput(cursorX, cursorY, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, leftClick);
                            }
                        }
                        
                    }
                    else if (TrackMode == "Right")
                    {
                        // The first found/tracked skeleton moves the mouse cursor.
                        //// Le premier squelette détecté a le contrôle de la souris.
                        if (sd.TrackingState == SkeletonTrackingState.Tracked)
                        {

                            // Tracks the right hand.
                            //// Suit les mouvements de la main droite.
                            if (sd.Joints[JointType.HandRight].TrackingState == JointTrackingState.Tracked)
                            {
                                // Create local variable to facilitate referring to the primary(?) skeleton right wrist.
                                // Scaling of the wrist's movements to fit the screen.
                                //// Créé une variable locale plus accessible pour le poignet droit du squelette principal.
                                //// Echelonne les mouvements du poignet pour correspondre à la taille de l'écran.
                                var wristRight = sd.Joints[JointType.WristRight];
                                var scaledRightHand = wristRight.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                                // Créé une variable locale plus accessible pour la tête du squelette principal.
                                // Echelonne les mouvements de la tête pour correspondre à la taille de l'écran.
                                var head = sd.Joints[JointType.Head];
                                var scaledHead = head.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                                // Creates more convenient local variables for the scaled right hand.
                                //// Créé des variables plus accessibles pour la position échelonnée de la main droite.
                                var cursorX = ((int)scaledRightHand.Position.X);
                                var cursorY = ((int)scaledRightHand.Position.Y);

                                // This is the part where we run the function that checks whether the user is trying to click.
                                // The result is sent to the cursor using the native method mojo.
                                //// Appel à la fonction qui vérifie si l'utilisateur essaye de détendre le bouton de la souris.
                                var leftClick = CheckForClickHold(scaledRightHand, scaledHead);

                                NativeMethods.SendMouseInput(cursorX, cursorY, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, leftClick);
                            }
                        }
                        
                    }
                    else if (TrackMode == "Body")
                    {
                        // The first found/tracked skeleton moves the mouse cursor.
                        //// Le premier squelette détecté a le contrôle de la souris.
                        if (sd.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            //// Suit les mouvements du corps.
                            if (sd.Joints[JointType.ShoulderCenter].TrackingState == JointTrackingState.Tracked)
                            {
                                var body = sd.Joints[JointType.ShoulderCenter];
                                var scaledBody = body.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                                var cursorX = (int)(((SystemParameters.PrimaryScreenWidth  - scaledBody.Position.X)/2) + (SystemParameters.PrimaryScreenWidth * 0.25));
                                var cursorY = ((int)(SystemParameters.PrimaryScreenHeight * 0.25));

                                var bodyDrag = CheckForBodyForward(scaledBody);

                                NativeMethods.SendMouseInput(cursorX, cursorY, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, bodyDrag);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Segment of code that checks and attributes clicking to the action of pushing your right hand out in front of your face.
        /// // Vérifie et attribute l'action de détente du bouton gauche de la souris à l'action de l'utilisateur poussant sa main devant sa tête.
        /// </summary>
        /// <param name="hand"></param>
        /// <param name="head"></param>
        /// <returns></returns>
        private bool CheckForClickHold(Joint hand, Joint head)
        {
            // Creates convenient local variables for the scaled hand's position.
            //// Créé des variables locales pour la position échelonnée de la main.
            var x = hand.Position.X;
            var y = hand.Position.Y;

            // More convenient local variables.
            //// D'autres variables locales pour faciliter leur accès.
            var screenwidth = (int)SystemParameters.PrimaryScreenWidth;
            var screenheight = (int)SystemParameters.PrimaryScreenHeight;

            // Test de la profondeur de la tête contre celle de la main.
            // Il faut au moins 0.3m de différence pour actionner le click.
            if (head.Position.Z - hand.Position.Z > 0.25)
            {
                return true;
            }

            return false;
        }

        private bool CheckForBodyForward(Joint body) 
        {
            if (body.Position.Z < 1.75)
            {
                return true;
            }

            return false;
        }
    }

    enum Mode
    {
        Color,
        Depth
    }
}
