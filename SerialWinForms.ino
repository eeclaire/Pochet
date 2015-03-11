// Connexion pour motor shield Pololu A4988
// Relier Reset et Sleep
// Connecter VDD au pin 5V output de l'Arduino
// Connecter GND au pin GND de l'Arduino GND (GND à côté de VDD)
// Connecter 1A et 1B à la spire 1 (fils blanc et rouge)*
// Connecter 2A et 2B à la spire 2 (fils jaune et bleu)*
// Connecter VMOT à l'alim (cathode)
// Connecter GRD à l'alim (anode)
// voir Feuille d'instruction du moteur si besoin

// Inclure la bibliothèque des moteur pas-à-pas
#include <Stepper.h>

int stp = 3;  // connecter pin 3 à step
int dir = 4;  // connecter pin 4 à dir
int ena = 6;  // connecter pin 6 à ena

Stepper motor(200, stp, dir);  // initialization du moteur pas-à-pas

void setup() 
{ 
  Serial.begin(9600);  // initialization de la communication en série
  
  pinMode(stp, OUTPUT);  // déclaration du pin de pas
  pinMode(dir, OUTPUT);  // déclaration du pin de direction
  pinMode(ena, OUTPUT);  // déclaration du pin d'authorization 
  
  motor.setSpeed(5);  // déclaration de la vitesse de rotation du moteur
  
  digitalWrite(ena, HIGH);  // retire l'autorization de tourner du moteur
}


void loop() 
{ 
  // attend un message série 
   if (Serial.available())
   {    
     digitalWrite(ena, LOW);  // rend l'autorization de tourner au moteur
     
     int steps = Serial.read();  // le nombre de pas doit être obtenu du message

     if (steps > 0 && steps < 128)  // si le numéro est positif (unsigned int)
     {
       motor.step(steps);
       Serial.write(-1);
     }
     else if (steps > 0 && steps > 128)  // si le numéro est négatif (signed int)
     {
       steps = 256 - steps;
       motor.step(-steps);
       Serial.write(1);
     }
   }
  
  digitalWrite(ena, HIGH);  // retire l'autorization de tourner du moteur
}
