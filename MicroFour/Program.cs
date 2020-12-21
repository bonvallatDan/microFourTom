/*
 * Projet : MicroFour
 * Auteur : Tom Andrivet & Thibault Capt & Ludovic Roux
 */
using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using device = GHI.Pins.FEZSpider;
using eCharDisplay;

namespace MicroFour
{
    public class Program
    {
        /// <summary>
        /// Enumération qui permet de définir l'état du programme
        /// </summary>
        private enum Etat { REG_INACTIF = 0, REG_TEMP = 1, REG_GAIN = 2}

        /// <summary>
        /// Constante définissant le dernier état avant le retour a l'état 1
        /// </summary>
        private const Etat LAST_ETAT = Etat.REG_GAIN;

        /// <summary>
        /// Constante définissant le temps de delai de la boucle while
        /// </summary>
        private const int DELAY = 100;

        /// <summary>
        /// Constante définissant en milliseconde le temps avant la mise en mode inactif
        /// </summary>
        private const int TIMEOUT = 20000;

        /// <summary>
        /// Constante définissant la tension du signal analogique
        /// </summary>
        private const double TENSION = 3.3;

        /// <summary>
        /// Constante définissant le coefficient de température (10mV/°C)
        /// </summary>
        private const double CT = 0.01;

        /// <summary>
        /// Constante définissant la tension de sortie (0°C = 500mV)
        /// </summary>
        private const double VOC = 0.5;

        /// <summary>
        /// Constante définissant l'incrément ou le décrément de la température maximale
        /// </summary>
        private const double STEP_TEMP = 1;


        /// <summary>
        /// Variable "Constante" définissant les étapes du gain
        /// </summary>
        private static double[] STEP_GAIN = new[] {0.01, 0.1, 1};

        /// <summary>
        /// Définition et instanciation du bouton de sélection des modes
        /// </summary>
        private static InputPort btnSel = new InputPort(device.Socket9.Pin3, false, Port.ResistorMode.Disabled);

        /// <summary>
        /// Définition et instanciation du bouton d'augmentation de la valeur
        /// </summary>
        private static InputPort btnAug = new InputPort(device.Socket11.Pin3, false, Port.ResistorMode.Disabled);

        /// <summary>
        /// Définition et instanciation du bouton de diminution de la valeur
        /// </summary>
        private static InputPort btnDim = new InputPort(device.Socket4.Pin3, false, Port.ResistorMode.Disabled);

        /// <summary>
        /// Définition et instanciation de la sonde de température
        /// </summary>
        private static AnalogInput sonde = new AnalogInput(device.Socket10.AnalogInput3);

        /// <summary>
        /// Définition et instanciation du corp de chauffe
        /// </summary>
        private static PWM corpChauffe = new PWM(device.Socket8.Pwm7, 10, 0, false);


        /// <summary>
        /// Définition de l'API pour gérer le CharDisplay
        /// </summary>
        private static MyCharDisplay display;

        /// <summary>
        /// Date qui stoque le moment du retour vers le mode inactif
        /// </summary>
        private static DateTime timeout;

        /// <summary>
        /// Date qui permet de compenser le temps d'execution du programme dans le délai DELAY
        /// </summary>
        private static DateTime compensation;

        /// <summary>
        /// Boolean qui défini si le timeout est actif
        /// </summary>
        private static bool isTimeout;

        /// <summary>
        /// Variable qui stoque le voltage renvoyé par la sonde
        /// </summary>
        private static double Vsonde = 0;

        /// <summary>
        /// Variable qui stoque la température calculée
        /// </summary>
        private static double Ta = 0;

        /// <summary>
        /// Ancienne valeur de la température Ta
        /// </summary>
        private static double oldTa = 0;

        /// <summary>
        /// Variable qui stoque la température maximale a atteindre
        /// </summary>
        private static double ta_max = 50;

        /// <summary>
        /// Ancienne valeur de la température maximale ta_max
        /// </summary>
        private static double oldta_max = 0;

        /// <summary>
        /// Variable qui stoque l'index du tableau STEP_GAIN du régulateur
        /// </summary>
        private static int gain = 0;

        /// <summary>
        /// Ancienne valeur de la variable gain
        /// </summary>
        private static double oldGain = 0;

        /// <summary>
        /// Variable stoquant l'état actuel du programme
        /// </summary>
        private static Etat etat = Etat.REG_INACTIF;

        /// <summary>
        /// Ancienne valeur de etat
        /// </summary>
        private static Etat oldEtat = Etat.REG_TEMP;

        /// <summary>
        /// Variable qui stoque les valeurs des boutons
        /// </summary>
        private static bool valBtnSel, valBtnAug, valBtnDim;

        /// <summary>
        /// Anciennes valeurs des boutons
        /// </summary>
        private static bool oldValBtnSel, oldValBtnAug, oldValBtnDim;

        /// <summary>
        /// Fonction principale
        /// </summary>
        public static void Main()
        {
            // Instanciation de l'API du CharDisplay, sur une FEZSPIDER 1 SOCKET 12
            display = new MyCharDisplay(1, 12, true);

            // Affichage des charactères qui ne changent jamais
            DisplayInit();

            // Boucle principale du programme
            while (true)
            {
                // Mise à jour de la Date pour la compensation en fin d'itération
                compensation = DateTime.Now;

                /*
                 * Récuperation des valeurs des boutons et de la sonde
                 */
                Vsonde = sonde.Read();
                valBtnSel = !btnSel.Read();
                valBtnAug = !btnAug.Read();
                valBtnDim = !btnDim.Read();

                // Calcul de la température a partir de la sonde
                Ta = System.Math.Round((Vsonde * TENSION - VOC) / CT);

                // Assignation a l'état inactif si le timeout est dépassé
                if (isTimeout && DateTime.Now > timeout)
                {
                    isTimeout = false;
                    etat = Etat.REG_INACTIF;
                }

                // Verification et correction de la température du corp de chauffe
                TempCheck();

                // Activation des éventuelles actions du bouton de sélection des modes
                BtnClick(valBtnSel, oldValBtnSel, false, true, true);

                // Ne pas activer les autres boutons si l'état est inactif (Inutile de verifier un truc non utiliser)
                if (etat != Etat.REG_INACTIF)
                {
                    // Activation des éventuelles actions du bouton d'augmentation
                    BtnClick(valBtnAug, oldValBtnAug, true, false);

                    // Activation des éventuelles actions du bouton de diminition0.
                    BtnClick(valBtnDim, oldValBtnDim, false, false);
                }

                Display();

                /*
                 * Assignation des nouvelles valeurs dans les anciennes
                 */
                oldValBtnSel = valBtnSel;
                oldValBtnAug = valBtnAug;
                oldValBtnDim = valBtnDim;
                oldTa = Ta;
                oldta_max = ta_max;
                oldGain = gain;
                oldEtat = etat;

                // Calcul du temps d'execution de l'itération actuelle
                int comp = (int)((DateTime.Now - compensation).Ticks / 10000.0);

                // Si le délai d'attente est inféreur au temps d'execution, attendre, sinon, laisser directement itéré à nouveau
                if (DELAY > comp) Thread.Sleep(DELAY - comp);
            }
        }

        /// <summary>
        /// Fonction qui permet d'afficher le gain
        /// </summary>
        private static void DisplayGain() {
            display.SetCursor(10, 0);
            string s = "G:" + getLength(STEP_GAIN[gain].ToString(), 4);
            display.WriteString(s);
        }

        /// <summary>
        /// Fonction qui permet d'afficher la température actuelle
        /// </summary>
        private static void DisplayTemp()
        {
            display.SetCursor(2, 1);
            display.WriteString(getLength(Ta.ToString(), 3));
        }

        /// <summary>
        /// Fonction qui permet d'afficher la température maximale
        /// </summary>
        private static void DisplayTempMax()
        {
            display.SetCursor(9, 1);
            display.WriteString(getLength(ta_max.ToString(), 3));
        }

        /// <summary>
        /// Fonction qui permet d'afficher l'état actuel du programme
        /// </summary>
        private static void DisplayEtat()
        {
            display.SetCursor(5, 0);
            display.WriteString(getLength(getText(etat), 11));
            if (etat == Etat.REG_GAIN) DisplayGain();
        }

        /// <summary>
        /// Fonction qui permet d'afficher ce qui doit être mit à jour sur le chardisplay (fonction centrale)
        /// </summary>
        private static void Display()
        {
            if (oldEtat     != etat    )                         DisplayEtat();
            if (oldGain     != gain    && etat == Etat.REG_GAIN) DisplayGain();
            if (oldTa       != Ta      )                         DisplayTemp();
            if (oldta_max != ta_max)                         DisplayTempMax();
        }

        /// <summary>
        /// Fonction qui affiche le template du chardisplay (charactères qui ne changent jamais)
        /// </summary>
        private static void DisplayInit()
        {
            display.SetCursor(0, 0);
            display.WriteString("Etat:");
            display.SetCursor(0, 1);
            display.WriteString("T:");
            display.SetCursor(6, 1);
            display.WriteString("TM:");
        }

        /// <summary>
        /// Fonction qui permet d'obtenir un string de la longueur que l'on veut (n'est pas tronqué si plus grand que demandé).
        /// Ajout d'espace si plus petit que demandé.
        /// </summary>
        /// <param name="s">String à agrandir</param>
        /// <param name="length">Taille a obtenir</param>
        /// <returns>String de la taille x</returns>
        private static string getLength(string s, int length)
        {
            string ret = s;
            for (int i = 0; i < length - s.Length; i++)
            {
                ret += " ";
            }
            return ret;
        }

        /// <summary>
        /// Fonction qui permet d'obtenir le texte a affiché a partir de l'état
        /// </summary>
        /// <returns>String correspondant à l'état en paramètre</returns>
        private static string getText(Etat etat)
        {
            switch (etat)
            {
                case Etat.REG_INACTIF: return "Inactif";
                case Etat.REG_GAIN:    return "Gain";
                case Etat.REG_TEMP:    return "Temperature";
                default:               return "";
            }
        }

        /// <summary>
        /// Fonction qui permet l'activation et la verification d'un bouton
        /// </summary>
        /// <param name="val">Valeur du bouton</param>
        /// <param name="oldVal">Ancienne valeur du bouton</param>
        /// <param name="increment">Faut-t-il incrémenter la valeur ?</param>
        /// <param name="detectFlan">Détection de flan activée ?</param>
        /// <param name="isSelect">Est-ce le bouton de sélection ?</param>
        private static void BtnClick(bool val, bool oldVal, bool increment, bool detectFlan, bool isSelect = false)
        {
            // Verification si le bouton est appuyé ou est enfoncé
            if ((!detectFlan || !oldVal) && val)
            {
                // Mise à jour du timeout
                isTimeout = true;
                timeout = DateTime.Now.AddMilliseconds(TIMEOUT);

                // En cas d'autre bouton que le bouton de sélection
                if (!isSelect)
                    switch (etat)
                    {
                        case Etat.REG_TEMP: // Incrémentation de la variable de température maximale
                            ta_max += (increment ? STEP_TEMP : -STEP_TEMP);
                            return;
                        case Etat.REG_GAIN: // Incrémentation de la variable de gain
                            gain += (increment ? 1 : -1);

                            // Retour a zéro si le gain dépasse le tableau STEP_GAIN
                            if (gain >= STEP_GAIN.Length) gain = 0;

                            // Retour au max d'index de STEP_GAIN si le gain dépasse 0
                            if (gain < 0) gain = STEP_GAIN.Length-1;
                            return;
                    }

                // Si c'est le bouton de sélection, incrémenter l'état ou le remettre a l'état d'origine (1)
                etat++;
                if (etat > LAST_ETAT)
                    etat = (Etat)1;
            }
        }

        /// <summary>
        /// Fonction qui met à jour le corp de chauffe en fonction de la température maximale, du gain, et de la température actuelle
        /// </summary>
        private static void TempCheck()
        {
            corpChauffe.DutyCycle = (1 - (Ta / ta_max)) * STEP_GAIN[gain];
        }
    }
}
