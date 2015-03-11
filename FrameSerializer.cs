using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kinect.Server
{
    /// <summary>
    /// Converts a Kinect frame into an HTML5 blob.
    /// // Convertit une trame d'image Kinect en un blob HTML5.
    /// </summary>
    public static class FrameSerializer
    {
        /// <summary>
        /// Maximum depth distance.
        /// // Profondeur maximale.
        /// </summary>
        static readonly float MAX_DEPTH_DISTANCE = 4095;

        /// <summary>
        /// Minimum depth distance.
        /// // Profondeur minimale.
        /// </summary>
        static readonly float MIN_DEPTH_DISTANCE = 850;

        /// <summary>
        /// Maximum depth distance offset.
        /// // Correction profondeur maximale.
        /// </summary>
        static readonly float MAX_DEPTH_DISTANCE_OFFSET = MAX_DEPTH_DISTANCE - MIN_DEPTH_DISTANCE;

        /// <summary>
        /// Default name for temporary color files.
        /// // Nom défaut du fichier temporaire couleur.
        /// </summary>
        static readonly string CAPTURE_FILE_COLOR = "Capture_Color.jpg";

        /// <summary>
        /// Default name for temporary depth files.
        /// // Nom défaut du fichier temporaire profondeur.
        /// </summary>
        static readonly string CAPTURE_FILE_DEPTH = "Capture_Depth.jpg";

        /// <summary>
        /// The color bitmap source.
        /// // Source de la bitmap couleur.
        /// </summary>
        static WriteableBitmap _colorBitmap = null;

        /// <summary>
        /// The depth bitmap source.
        /// // Source de la bitmap profondeur.
        /// </summary>
        static WriteableBitmap _depthBitmap = null;

        /// <summary>
        /// The RGB pixel values.
        /// // Valeurs des pixels RVB.
        /// </summary>
        static byte[] _colorPixels = null;

        /// <summary>
        /// The RGB depth values.
        /// // Valeurs de profondeurs RVB.
        /// </summary>
        static byte[] _depthPixels = null;

        /// <summary>
        /// The actual depth values.
        /// // Véritables valeurs de profondeur.
        /// </summary>
        static short[] _depthData = null;

        
        /// <summary>
        /// Method to serialize the color frame.
        /// // Méthode pour sérializer la trame d'image couleur.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public static byte[] Serialize(this ColorImageFrame frame)
        {
            // Create bitmap.
            //// Création de la bitmap.
            var format = PixelFormats.Bgra32;
            int width = frame.Width;
            int height = frame.Height;
            int stride = width * format.BitsPerPixel / 8;

            // Handle the case in which the colorbitmap is empty. Prepare the rgb color pixel values and the color bitmap source.
            //// S'occuper du cas dans lequel la bitmap couleur est vide. Prépare les valeurs de pixels couleur RVB et la source de la bitmap couleur. 
            if (_colorBitmap == null)
            {
                _colorPixels = new byte[frame.PixelDataLength];
                _colorBitmap = new WriteableBitmap(width, height, 96.0, 96.0, format, null);
            }

            // Copy the frame data to the color pixels.
            //// Copie les données de la trame d'image aux pixels de couleurs.
            frame.CopyPixelDataTo(_colorPixels);

            // Update the color pixels in the bitmap.
            //// Renouvèle les pixels de couleur dans la bitmap.
            _colorBitmap.WritePixels(new Int32Rect(0, 0, width, height), _colorPixels, stride, 0);

            return CreateBlob(_colorBitmap, CAPTURE_FILE_COLOR);
        }

        
        /// <summary>
        /// Method to serialize the depth frame.
        /// // Méthode pour sérializer la trame d'image de profondeur.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public static byte[] Serialize(this DepthImageFrame frame)
        {
            // Create bitmap.
            //// Créer la bitmap.
            var format = PixelFormats.Bgra32;
            int width = frame.Width;
            int height = frame.Height;
            int stride = width * format.BitsPerPixel / 8;

            // Handle the case in which the depthbitmap is empty. Prepare the depth pixel values and the depth bitmap source.
            //// S'occupe du cas où la bitmap de profondeur est vide. Prépare les valeurs de pixels de profondeur et la source de la bitmap de profondeur.
            if (_depthBitmap == null)
            {
                _depthData = new short[frame.PixelDataLength];
                _depthPixels = new byte[height * width * 4];
                _depthBitmap = new WriteableBitmap(width, height, 96.0, 96.0, format, null);
            }

            // Copy the frame pixel data to the depth data array.
            //// Copie les données de pixels de trame d'image dans le tableau de données de profondeur.
            frame.CopyPixelDataTo(_depthData);

            for (int depthIndex = 0, colorIndex = 0; depthIndex < _depthData.Length && colorIndex < _depthPixels.Length; depthIndex++, colorIndex += 4)
            {
                // Get the depth value.
                //// Obtiens les valeurs de profondeur.
                int depth = _depthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                // Equal coloring for monochromatic histogram.
                //// Egalize les coloris pour un histogrmme monochromatique. (Utilise des couleurs pour représenter l'échelle de profondeur.)
                byte intensity = (byte)(255 - (255 * Math.Max(depth - MIN_DEPTH_DISTANCE, 0) / (MAX_DEPTH_DISTANCE_OFFSET)));

                _depthPixels[colorIndex + 0] = intensity;
                _depthPixels[colorIndex + 1] = intensity;
                _depthPixels[colorIndex + 2] = intensity;
            }

            // Update the pixels in the depth bitmap.
            //// Renouvelle les pixels dans la bitmap de profondeur.
            _depthBitmap.WritePixels(new Int32Rect(0, 0, width, height), _depthPixels, stride, 0);

            return CreateBlob(_depthBitmap, CAPTURE_FILE_DEPTH);
        }

      
        /// <summary>
        /// Method to encode the input bitmap data into a string format more easily passed to the websocket.
        /// // Méthode pour encoder les données de bitmap en un format en chaîne plus facilement passé au websocket.
        /// </summary>
        /// <param name="bitmap"> INPUT </param>
        /// <param name="file"> OUTPUT </param>
        /// <returns></returns>
        public static byte[] CreateBlob(WriteableBitmap bitmap, string file)
        {
            // Create a new image stream encoder.
            //// Créé un nouvel encodeur d'image stream.
            BitmapEncoder encoder = new JpegBitmapEncoder();

            // Pile into the encoder the data from the source bitmap.
            //// Empile dans l'encodeur les données de la bitmap source.
            encoder.Frames.Add(BitmapFrame.Create(bitmap as BitmapSource));

            // Add to the encoder the OS instructions for opening the file.
            //// Ajouter à l'encodeur les instructions d'ouverture du fichier.
            using (var stream = new FileStream(file, FileMode.Create))
            {
                encoder.Save(stream);
            }

            // Convert saved bitmap to blob.
            //// Convertit la bitmap en blob de données.
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    return reader.ReadBytes((int)stream.Length);
                }
            }
        }
    }
}
