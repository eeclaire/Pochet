/*------------------------------------------------------------------------------
 * Claire Durand
 * Pochet du Courval
 * 
 * Projet de numérisation des panoplies
 * 
 * Juin/Juillet/Août 2014
 * 
 * Ce programme est destiné à automatiser la capture et la nomenclature de photos afin d'obtenir tout les angles d'un flacon.
 * La capture de photos est destinée à être utilisée en coordination avec un plateau tournant motorisé et son système de circuit.
 * La nomenclature des photos obtenues est basée sur leur assemblage dans une animation html/javascript/jQuery (voir dossier Animations).
 * 
 * Ce programme a été écrit en utilisant comme base le projet ColorBasics- WPF inclu dans le Kinect for Windows Developer Toolkit Browser v1.8.0.
 * 
 * 
 * Dans cette version,  l'idée est de simplement cliquer sur l'une des deux flèches pour déclencher un tour complet du moteur 
 * et la capture de photos. La prise de photos est dépendante de la rotation du moteur. L'appli ne prend qu'une capture qu'après 
 * avoir reçu confirmation d'une incrémentation de pas du plateau et un message de l'Arduino contenant la direction de la rotation.
 * Ce message de confirmation participe aussi à la nomenclature de chaque capture.
 * 
 * <copyright file="MainWindow.xaml.cs" company="Microsoft">
 *     Copyright (c) Microsoft Corporation.  All rights reserved.
 * </copyright>
 *
 * 
 *------------------------------------------------------------------------------
*/

namespace Microsoft.Samples.Kinect.ColorBasics
{
    using System;
    using System.Collections.Generic;   
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Text;  
    using System.Threading; 
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using System.Windows.Forms;
    using MessageBox = System.Windows.Forms.MessageBox;  
    using Microsoft.Kinect;
    using System.IO.Ports;  
 
   

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// // Logique d'interaction avec MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public KinectSensor sensor;     // capteur Kinect actif
        private WriteableBitmap colorBitmap;    // bitmap contenant l'information de l'image couleur
        private byte[] colorPixels;     // stockage des données de l'image couleur

        SerialPort currentPort;     // port série actif

        public bool acquire = false;    // condition de capture photo
        public bool turn = false;       // condition de rotation plateau
        public bool leftTurn = false;   // condition de rotation dans le sens inverse des aiguilles d'une montre
        public bool rightTurn = false;  // condition de rotation dans le sens des aiguilles d'une montre
        
        public string filepath = null;  // chemin d'accès au dossier de sauvegarde des photos 

        public int stepCount = 0;   // compteur des pas pris (avec direction)    
        public int photocount = 0;  // compteur de la valeur absolue des pas pris
        public int row = 0;     // numéro de rangée
        public int i = 0;   // compteur de pas (déprécié)

        public int photosPerRow;    // input: nombre photos par rangée
        public int stepsPerPhoto;   // output: nombre de quart de pas à prendre entre chaque photo

        /// Début des fonctions de setup.
        /// Note: SensorColorFrameReady à proprement parler est un gestionnaire d'évènement. Sa défintion a été inclus 
        /// dans les fonctions de setup parce que WindowLoaded s'abonne à l'évènement de ColorFrameReady pour le capteur.
        /// 
        public MainWindow()
        {
            InitializeComponent();
            setCommPort();
        }   // initialization et ouverture de la fenêtre Windows

        public void setCommPort() 
        {
            try
            {
                string[] ports = SerialPort.GetPortNames(); // Obtient tout les noms de port de communication (COM3)
                foreach (string port in ports)  //Teste chaque port  
                {
                    currentPort = new SerialPort(port, 9600);   // Etablit la communication en série avec l'Arduino
                    MessageBox.Show("current port is ... " + currentPort);
                }
            }
            catch (Exception) 
            {
                MessageBox.Show("Aucun port de communication en série repéré. Veuillez vous assurer qu'une carte Arduino est branchée.");
            }
        }   // établissement de la communication en série avec l'Arduino

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            foreach (var potentialSensor in KinectSensor.KinectSensors)     // Teste chaque capteur Kinect potentiel
            {
                if (potentialSensor.Status == KinectStatus.Connected)   
                {
                    this.sensor = potentialSensor;      // Etablit le capteur actif à utiliser
                    break;
                }
            }

            if (null != this.sensor)
            {
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution1280x960Fps12);    // mise en place du streaming de l'image couleur

                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.Image.Source = this.colorBitmap;   // obtient les données du stream couleur 

                this.sensor.ColorFrameReady += this.SensorColorFrameReady;  // souscription à l'évènement du renouvellement de l'image entrante

                try
                {
                    this.sensor.Start();        // démarrage du capteur
                    this.sensor.ElevationAngle = 0;     // mise à 0 de l'angle d'élévation du capteur
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }   
            
            // Prévient l'utilisateur s'il n'y a pas de Kinect prête à l'emploi.
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }   // exécution des tâches de démarrage

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.ElevationAngle = 0;     // mise à 0 de l'élévation de l'angle du capteur
                this.sensor.Stop();         // arrêt du capteur
            }
            if (currentPort.IsOpen)
            {
                currentPort.Close();    // fermeture du port de communication série si ouvert
            }
               
        }   // tâches de fermeture du programme

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // fonction de vérification des conditions de capture et de rotation
                    photoCheck();

                    // renouvellement de la fenêtre de l'appli pour refléter le renouvellement d'image
                    colorFrame.CopyPixelDataTo(this.colorPixels);    

                    this.colorBitmap.WritePixels(               
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);

                    // fonction de vérification des conditions de capture et de rotation
                    photoCheck();                     
                }
            } 
        }   // gestionnaire d'évènement pour chaque nouvelle trame disponible (revouvellement continu)
        ///
        /// Fin du setup.


        /// Début des fonctions et méthodes de paramétrage.
        /// 
        private void set_Tilt_Click(object sender, RoutedEventArgs e)
        {
            if (angle_text.Text != string.Empty)
            {
                int angle = Convert.ToInt32(angle_text.Text);   // Convertit l'entrée de l'utilisateur à un nombre entier.

                // Vérification que l'angle entré est dans les limites de l'élévation de la Kinect.
                if (angle <= sensor.MaxElevationAngle && angle >= sensor.MinElevationAngle) 
                {
                    sensor.ElevationAngle = angle;
                }
                else
                {
                    MessageBox.Show("Veuillez entrer une valeur d'élévation dans les limites de la Kinect (-27° to 27°)");
                }
            }
        }   // modification programmatique de l'angle de la Kinect

        private void set_Row_Click(object sender, RoutedEventArgs e)
        {
            if (row_text.Text != string.Empty)      // Agit seulement si l'utilisateur entre quelque chose dans la boite correspondante.
            {
                row = Convert.ToInt32(row_text.Text);   // Convertion de l'entrée de l'utilisateur à un nombre entire et sauvegarde dans la variable de la rangée.
            }
        }   // changement de rangée de l'objet

        private void new_obj_btn_Click(object sender, RoutedEventArgs e)
        {
            acquire = false;    // cessation de la sauvegarde des images
            turn = false;       // cessation de la sauvegarde des images
         
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();   // boîte dialogue Windows pour le choix du dossier de sauvegarde 
            folderDialog.RootFolder = Environment.SpecialFolder.MyPictures;     // etablissement du fichier où démarre la sélection
            folderDialog.ShowDialog();  // ouverture de la boîte dialogue du choix de dossier sauvegarde

            filepath = folderDialog.SelectedPath;   // chemin de fichier sélectioné
        }   // option de nouvel objet, choix du dossier de sauvegarde

        private void comboBox1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // choix du nombre de photos à prendre par rangée
            if (comboBox1.SelectedItem == Eight)
                photosPerRow = 800;
            else if (comboBox1.SelectedItem == Four)
                photosPerRow = 400;
            else if (comboBox1.SelectedItem == Two)
                photosPerRow = 200;
            else if (comboBox1.SelectedItem == Sixteen)
                photosPerRow = 160;
            else if (comboBox1.SelectedItem == One)
                photosPerRow = 100;
            else if (comboBox1.SelectedItem == Eighty)
                photosPerRow = 80;

            stepsPerPhoto = 800 / photosPerRow;     // calculs du nombre de quart de pas à prendre entre chaque photo
        }   // choix du nombre de photos à prendre par rangée
        ///
        /// Fin des fonctions et méthodes de paramétrage.


        /// Début des gestionnaires d'évènements et fonctions plus évènementielles.
        /// 
        private void right_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            turn = true;
            acquire = true;
            rightTurn = true;
            leftTurn = false;
            photoCheck();
        }   // gestionnaire d'évènement pour la détente de la flèche droite (rotation plateau dans la sens des aiguilles d'une montre)

        private void left_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            turn = true;
            acquire = true;
            leftTurn = true;
            rightTurn = false;
            photoCheck();
        }   // gestionnaire d'évènement pour la détente de la flèche gauche (rotation plateau inverse)

        private void saveSingleShot(int col)
        {
                if (null == this.sensor)
                {
                    this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                    return;
                }

                BitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string default_path = Path.Combine(myPhotos, "frame" + row + "_" + col + ".jpg");
                

                if (filepath != null)
                {
                    string path = Path.Combine(filepath, "frame" + row + "_" + col + ".jpg");

                    try
                    {
                        using (FileStream fs = new FileStream(path, FileMode.Create))
                        {
                            encoder.Save(fs);
                        }
                        // Modifie le message dans la "Status bar" dans le coin bas-gauche de la fenêtre pour indiquer un succès de sauvegarde.
                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
                    }
                    catch (IOException)
                    {
                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
                    }
                }
                else
                {
                    try
                    {
                        using (FileStream fs = new FileStream(default_path, FileMode.Create))
                        {
                            encoder.Save(fs);
                        }

                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, default_path);
                    }
                    catch (IOException)
                    {
                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, default_path);
                    }
                }
        }   // sauvegarde d'une prise d'écran unique

        private int shiftReturn()       // envoi à l'Arduino du nombre de pas à prendre et décryption du message renvoyé
        {
            if (!currentPort.IsOpen)
            {
                try
                {
                    currentPort.Open();     // ouverture du port de communication en série
                }
                catch (Exception)
                {
                    MessageBox.Show("There was a problem opening the serial port.");
                }
            }

            try
            {
                // Envoi de la commande de rotation au moteur. 
                // La variable "outgoing" contient la taille du pas à prendre selon le nombre de quarts de pas à prendre entre chaque photo.
                byte[] outgoing = new byte[1];
                if (leftTurn == true)
                {
                   outgoing[0] = (byte)stepsPerPhoto;   // préparation du message de prise de pas positive, envoyé comme octet
                }
                else if (rightTurn == true)
                {
                    int step = 0 - stepsPerPhoto;

                    sbyte[] outgoingSigned = new sbyte[1];
                    outgoingSigned[0] = (sbyte)step;
                    outgoing[0] = (byte)outgoingSigned[0];  // préparation du message de prise de pas négatif, envoyé comme octet signé
                }
                else
                {
                    MessageBox.Show("Il y a erreur au niveau du nombre de directions validées. Veuillez ne cliquez qu'à gauche ou qu'à droite.");
                    leftTurn = false;   // arrêt des passages de commande
                    rightTurn = false;
                }
                
                currentPort.Write(outgoing, 0, outgoing.Length);    // envoi des octets de commande à l'Arduino


                // attente d'une réponse de l'Arduino
                sbyte[] incomingSigned = new sbyte[2];
                byte[] incoming = new byte[2];
                currentPort.Read(incoming, 0, incoming.Length); // lecture du message retour

                int returnMess = 0;
                incomingSigned[0] = (sbyte)incoming[0];
                returnMess = incomingSigned[0]; 
                currentPort.Close();
                return (returnMess);    // retour au programme du message de l'Arduino
            }
            catch (Exception)
            {

                MessageBox.Show("Right mouse down dialog failed");
                return (0);
            }
        }

        private void analyzeReturn(int returnMessage) 
        {
            int photoNameMax = photosPerRow - 1;    // l'index de 0 nous force à réduire d'un le numéro de chaque photo dans leur nomenclature

            // Les 2 premières possibilités sur ce if/else relève de boucler la séquence photo pour le wrap 
            // qui sera nécéssaire d'établir lorsqu'on voudra donner la possibilité à l'utilisateur de déplacer la souris dans les 2 sens.
            if (stepCount == 0 && returnMessage == -1) 
            {
                stepCount = photoNameMax;   // si on "décrémente" stepCount et qu'on arrive à 0, retourner au numéro de photo maximum
            }
            else if (stepCount == photoNameMax && returnMessage == 1)
            {
                stepCount = 0;  // si on incrémente stepCount et qu'on arrive au numéro de photo maximum, retourner à 0 pour boucler
            }
            else
            {
                stepCount = stepCount + returnMessage;  // incrémentation de la nomenclature des photos selon le message retour de l'Arduino
            }

            photocount++;   // incrémentation du nombre total de photos
            if (photocount == photosPerRow)
            {
                // arrête la rotation and la capture photo
                turn = false;
                acquire = false;
                leftTurn = false;
                rightTurn = false;

                // reinitialization des compteurs
                photocount = 0;
                stepCount = 0;
            }
        }   // analyze du message retourné par l'Arduino

        private void photoCheck() 
        {
            if (turn == true && acquire == true)
            {
                int messageBuffer = 0;
                messageBuffer = shiftReturn();  // envoi de commande à l'Arduino et retour du message de l'Arduino

                if (messageBuffer != 0)
                {
                    analyzeReturn(messageBuffer);   // analyze du message retour de l'Arduino pour la nomenclature des photos
                    saveSingleShot(stepCount);      // sauvegarde d'une prise de vue
                }
                else
                {
                    MessageBox.Show("Something went wrong with the message transmission in the photo check");
                }
            }
        }   // vérification des conditions de capture et de rotation, et implémentation du rhythme rotation-sauvegarde si approprié
        ///
        /// Fin des gestionnaires d'évènements et fonctions évènementielles.
    }
}