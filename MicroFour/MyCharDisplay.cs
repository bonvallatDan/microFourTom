/*
 * Projet : MyCharDisplay
 * Auteur : Tom Andrivet
 * Description : Classe pour géré un CharDisplay 16x2
 *
 */
using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;

namespace eCharDisplay
{
	// Classe principale
    class MyCharDisplay
    {
		//Type du MicroControlleur et numéro de socket du CharDisplay
        private int fezType, socketNum;
		
		//Déclaration des pins pour les utilisés
        private OutputPort rs, d7, d6, d5, d4, e, blacklight;
		
		//Déclaration du tableau des pins en fonction du socket (Ordre : RS,D4,D5,D6,D7,E,Blacklight)
        private Cpu.Pin[] pins;
		
		//Déclaration du rétr-éclairage du CharDisplay
        private bool backlightb;

		
		//Constructeur avec le type de MicroControlleur, le numéro du socket, et si il y a du rétro-éclairage
        public MyCharDisplay(int fezType, int socketNum, bool backlight)
        {
			//Sauvegarde des valeurs entrées dans le constructeur
            this.socketNum = socketNum;
            this.fezType = fezType;
            this.backlightb = backlight;

			//Switch du type de MicroControlleur et récuperation des pins en fonction du socket
			//Erreur si le type d'est pas supporté par ma classe
            switch (fezType)
            {
                case 1: pins = SocketFezSpiderI.getPinsBySocket(socketNum); break;
                case 2: pins = SocketFezSpiderII.getPinsBySocket(socketNum); break;
                default: throw new Exception("Invalid FEZSpider version");
            }
			
			//Si pins est null, c'est que ce n'était pas un socket Y (demandé pour que le CharDisplay fonctionne)
            if (pins == null) throw new Exception("Socket " + socketNum + " is not a valid Y socket type !");

			//Initialisation des ports (set des OutputPorts)
            initPort();
			
			//Attente du démarrage avant l'initialisation du screen
            Thread.Sleep(40);
			
			//Initialisation du screen
            initScreen();
        }

		//Initialisation du screen
        private void initScreen()
        {
            SendData("0x33"); //Function_Set 8bits
            SendData("0x32"); //Function_Set 4bits
            SendData("0x0C"); //Char_Display ON
            SendData("0x01"); //Clear_Display
        }

		//Initialisation des ports (les OutputPorts)
        private void initPort()
        {
            rs = new OutputPort(pins[0], false);
            d4 = new OutputPort(pins[1], false);
            d5 = new OutputPort(pins[2], false);
            d6 = new OutputPort(pins[3], false);
            d7 = new OutputPort(pins[4], false);
            e = new OutputPort(pins[5], false);
            blacklight = new OutputPort(pins[6], backlightb);
        }

		//Fonction pour creer un caractère custom sur le CharDisplay
        public void CreateCustomChar(int place, string[] custom)
        {
            SendRawByteCommand(CGRamPlaceFromDec(place, false));
            for (int i = 0; i < 8; i++)
            {
                SendRawByteData(binStringToHexByte(custom[i]));
            }
        }

        //Fonction pour convertir un binaire string en hex byte
        private byte binStringToHexByte(string bin)
        {
            int divise = 16;
            int cur = 0;
            foreach (char b in bin.ToCharArray())
            {
                cur += b=='1' ? divise : 0;
                divise /= 2;
            }
            return Byte.Parse(cur.ToString());
        }

        //Fonction pour récupéré la place ou la localisation du caractère custom
        private byte CGRamPlaceFromDec(int num, bool draw)
        {
            switch (num)
            {
                default:
                case 1:
                    return draw ? (byte)0x00 : (byte)0x40;
                case 2:
                    return draw ? (byte)0x01 : (byte)0x48;
                case 3:
                    return draw ? (byte)0x02 : (byte)0x50;
                case 4:
                    return draw ? (byte)0x03 : (byte)0x58;
                case 5:
                    return draw ? (byte)0x04 : (byte)0x60;
                case 6:
                    return draw ? (byte)0x05 : (byte)0x68;
                case 7:
                    return draw ? (byte)0x06 : (byte)0x70;
                case 8:
                    return draw ? (byte)0x07 : (byte)0x78;
            }
        }

        //Fonction pour creer un caractère custom sur le CharDisplay
        public void WriteCustomChar(int place)
        {
            SendRawByteData(CGRamPlaceFromDec(place, true));
        }

		//Fonction pour ecrire un String sur le CharDisplay
        public void WriteString(String str)
        {
			//Loop des caractères du string
            foreach (char c in str.ToCharArray())
            {
				//Envoie du caractère pour l'ecriture
                WriteChar(c);
            }
        }
		
		//Fonction pour ecrire un caractère sur le CharDisplay
        public void WriteChar(char c)
        {
			//Conversion du charactère en hexadecimal
			SendData("1x" + ((int)c).ToString("X"));
        }

		//Fonction qui clear le CharDisplay
        public void ClearScreen()
        {
            SendData("0x01");
        }

		//Fonction qui permet de set le curseur sur le CharDisplay
        public void SetCursor(int x, int y)
        {
			//Chiffre decimal qui sera converti en binaire a la fin
            int hecdec = 0;
			
			//Si le X ou le Y est supérieur au max du CharDisplay, on fait une erreur
            if (x >= 40 || y > 1)
                throw new Exception("X !>= 40 and Y !> 1 (X:" + x + "; Y:" + y + ")");
			
			//Si le Y est 0, on n'altère pas le chiffre final
            if (y == 0)
            {
                hecdec = x; //Set du chiffre final par x
            }
            else //Sinon on ajoute 64 au résultat (Pour faire une sorte de retour a la ligne)
            {
                hecdec = 64 + x;
            }
            SendRaw("01" + binary(hecdec, 7)); //Envoie des données en binaire au CharDisplay
        }

		//Fonction pour envoyé une commande en byte au CharDisplay
        public void SendRawByteCommand(byte b)
        {
			//Une commande = RS=0
            rs.Write(false);

			//Set des 4 premiers bits
            d7.Write((b & 0x80) == 0x80);
            d6.Write((b & 0x40) == 0x40);
            d5.Write((b & 0x20) == 0x20);
            d4.Write((b & 0x10) == 0x10);

			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);

			//Set des 4 derniers bits
            d7.Write((b & 0x8) == 0x8);
            d6.Write((b & 0x4) == 0x4);
            d5.Write((b & 0x2) == 0x2);
            d4.Write((b & 0x1) == 0x1);
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);
        }
		
		//Fonction pour envoyé une donnée en byte au CharDisplay
        public void SendRawByteData(byte b)
        {
			//Une Donnée = RS=1
            rs.Write(true);

			//Set des 4 premiers bits
            d7.Write((b & 0x80) == 0x80);
            d6.Write((b & 0x40) == 0x40);
            d5.Write((b & 0x20) == 0x20);
            d4.Write((b & 0x10) == 0x10);

			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);

			//Set des 4 derniers bits
            d7.Write((b & 0x8) == 0x8);
            d6.Write((b & 0x4) == 0x4);
            d5.Write((b & 0x2) == 0x2);
            d4.Write((b & 0x1) == 0x1);
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);
			
			//Remise a zero de RS
            rs.Write(false);
        }

		//Fonction pour envoyé un binaire au CharDisplay
        public void SendRaw(string bin)
        {
			//On converti en tableau de caractère le binaire
            char[] b = bin.ToCharArray();
			
			//Si le binaire est inferieur ou superieur a 9 bits on fait une erreur
            if (b.Length < 9 || b.Length > 9)
                throw new Exception("Invalid binary, he need 9 bits (" + bin + ")");
            
			//Set des 4 premiers bits
            rs.Write(intToBool(Convert.ToInt32(b[0].ToString())));
            d7.Write(intToBool(Convert.ToInt32(b[1].ToString())));
            d6.Write(intToBool(Convert.ToInt32(b[2].ToString())));
            d5.Write(intToBool(Convert.ToInt32(b[3].ToString())));
            d4.Write(intToBool(Convert.ToInt32(b[4].ToString())));
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);

			//Set des 4 derniers bits
            rs.Write(intToBool(Convert.ToInt32(b[0].ToString())));
            d7.Write(intToBool(Convert.ToInt32(b[5].ToString())));
            d6.Write(intToBool(Convert.ToInt32(b[6].ToString())));
            d5.Write(intToBool(Convert.ToInt32(b[7].ToString())));
            d4.Write(intToBool(Convert.ToInt32(b[8].ToString())));
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);
        }

		//Fonction pour envoyé sous forme de string le byte (au lieu de 0x40 on a "0x40")
        public void SendData(string code)
        {
			//Split entre la commande et la donnée
            string[] list = code.Split('x');
			
			//Liste de la donnée
            char[] c = list[1].ToCharArray();
			
			//convertion en bool de la commande
            bool command = intToBool(Convert.ToInt32(list[0]));
			
			//Transformation en decimal et ensuite en binaire du permier chiffre de la donnée (4 bits)
            string bin = binary(Convert.ToInt32(c[0].ToString(), 16), 4);
			
			//Split des bits du binaire du premier chiffre pour les 4 permiers bits
            char[] b = bin.ToCharArray();
			
			//Transformation en decimal et ensuite en binaire du dernier chiffre de la donnée (4 bits)
            bin = binary(Convert.ToInt32(c[1].ToString(), 16), 4);
			
			//Split des bits du binaire du dernier chiffre pour les 4 derniers bits
            char[] b1 = bin.ToCharArray();

			//Set de la commande si c'en est une
            rs.Write(command);
			
			//Set des 4 premiers bits
            d7.Write(intToBool(Convert.ToInt32(b[0].ToString())));
            d6.Write(intToBool(Convert.ToInt32(b[1].ToString())));
            d5.Write(intToBool(Convert.ToInt32(b[2].ToString())));
            d4.Write(intToBool(Convert.ToInt32(b[3].ToString())));
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);

			//Set de la commande si c'en est une
            rs.Write(command);
			
			//Set des 4 derniers bits
            d7.Write(intToBool(Convert.ToInt32(b1[0].ToString())));
            d6.Write(intToBool(Convert.ToInt32(b1[1].ToString())));
            d5.Write(intToBool(Convert.ToInt32(b1[2].ToString())));
            d4.Write(intToBool(Convert.ToInt32(b1[3].ToString())));
			
			//Activation de la mémorisation du CharDisplay
            e.Write(true);

			//Attente d'un ms pour qu'il enregistre
            Thread.Sleep(1);

			//On éteint E
            e.Write(false);
        }
		
		//Fonction pour convertir un chiffre decimal en binaire et sur combien de bit
        private string binary(int num, int binLong)
        {
            //Variable qui stock le code binaire final
            String binFinal = "";

            /*Variable qui sera utilisé pour calculer le binaire (on n'utilise pas 
             *sec directement car on veut afficher a la fin la secondes et le code qui correspond)*/
            int binsec = num;

            //Variable qui stock le 1 ou 0 a rajouter
            int bin;

            //Boucle qui s'execute tant que binsec est superieur a 0
            while (binsec > 0)
            {
                //On fait un modulo 2 de binsec
                bin = binsec % 2;

                //On divise binsec par 2
                binsec /= 2;

                //On ajoute le modulo fait avec le reste du binFinal deja calculer
                binFinal = bin.ToString() + binFinal;
            }

            //Boucle qui permet de rajouter les zero pour que les bits soit remplis
            while (binFinal.Length < binLong)
            {
                binFinal = '0' + binFinal;
            }

            return binFinal;
        }

		//Convertion entre un 0 ou 1 en false et true
        private bool intToBool(int i)
        {
            return i == 0 ? false : true;
        }
    }

	//Classe des sockets du MicroControlleur version 1
    public class SocketFezSpiderI
    {
		//Récuperation des pins en fonction du socket
        public static Cpu.Pin[] getPinsBySocket(int socket)
        {
            switch (socket)
            {
                case 5: return getSocket5Pins();
                case 6: return getSocket6Pins();
                case 8: return getSocket8Pins();
                case 9: return getSocket9Pins();
                case 11: return getSocket11Pins();
                case 12: return getSocket12Pins();
                case 14: return getSocket14Pins();
                default: return null;
            }
        }
		
		//Retourne les pins du socket 5
        public static Cpu.Pin[] getSocket5Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket5.Pin4,
                GHI.Pins.FEZSpider.Socket5.Pin5,
                GHI.Pins.FEZSpider.Socket5.Pin7,
                GHI.Pins.FEZSpider.Socket5.Pin9,
                GHI.Pins.FEZSpider.Socket5.Pin6,
                GHI.Pins.FEZSpider.Socket5.Pin3,
                GHI.Pins.FEZSpider.Socket5.Pin8
            };
        }
		//Retourne les pins du socket 6
        public static Cpu.Pin[] getSocket6Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket6.Pin4,
                GHI.Pins.FEZSpider.Socket6.Pin5,
                GHI.Pins.FEZSpider.Socket6.Pin7,
                GHI.Pins.FEZSpider.Socket6.Pin9,
                GHI.Pins.FEZSpider.Socket6.Pin6,
                GHI.Pins.FEZSpider.Socket6.Pin3,
                GHI.Pins.FEZSpider.Socket6.Pin8
            };
        }
		//Retourne les pins du socket 8
        public static Cpu.Pin[] getSocket8Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket8.Pin4,
                GHI.Pins.FEZSpider.Socket8.Pin5,
                GHI.Pins.FEZSpider.Socket8.Pin7,
                GHI.Pins.FEZSpider.Socket8.Pin9,
                GHI.Pins.FEZSpider.Socket8.Pin6,
                GHI.Pins.FEZSpider.Socket8.Pin3,
                GHI.Pins.FEZSpider.Socket8.Pin8
            };
        }
		//Retourne les pins du socket 9
        public static Cpu.Pin[] getSocket9Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket9.Pin4,
                GHI.Pins.FEZSpider.Socket9.Pin5,
                GHI.Pins.FEZSpider.Socket9.Pin7,
                GHI.Pins.FEZSpider.Socket9.Pin9,
                GHI.Pins.FEZSpider.Socket9.Pin6,
                GHI.Pins.FEZSpider.Socket9.Pin3,
                GHI.Pins.FEZSpider.Socket9.Pin8
            };
        }
		//Retourne les pins du socket 11
        public static Cpu.Pin[] getSocket11Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket11.Pin4,
                GHI.Pins.FEZSpider.Socket11.Pin5,
                GHI.Pins.FEZSpider.Socket11.Pin7,
                GHI.Pins.FEZSpider.Socket11.Pin9,
                GHI.Pins.FEZSpider.Socket11.Pin6,
                GHI.Pins.FEZSpider.Socket11.Pin3,
                GHI.Pins.FEZSpider.Socket11.Pin8
            };
        }
		//Retourne les pins du socket 12
        public static Cpu.Pin[] getSocket12Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket12.Pin4,
                GHI.Pins.FEZSpider.Socket12.Pin5,
                GHI.Pins.FEZSpider.Socket12.Pin7,
                GHI.Pins.FEZSpider.Socket12.Pin9,
                GHI.Pins.FEZSpider.Socket12.Pin6,
                GHI.Pins.FEZSpider.Socket12.Pin3,
                GHI.Pins.FEZSpider.Socket12.Pin8
            };
        }
		//Retourne les pins du socket 14
        public static Cpu.Pin[] getSocket14Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpider.Socket14.Pin4,
                GHI.Pins.FEZSpider.Socket14.Pin5,
                GHI.Pins.FEZSpider.Socket14.Pin7,
                GHI.Pins.FEZSpider.Socket14.Pin9,
                GHI.Pins.FEZSpider.Socket14.Pin6,
                GHI.Pins.FEZSpider.Socket14.Pin3,
                GHI.Pins.FEZSpider.Socket14.Pin8
            };
        }
    }
    
	//Classe des sockets du MicroControlleur version 2
	public class SocketFezSpiderII
    {
		//Récuperation des pins en fonction du socket
        public static Cpu.Pin[] getPinsBySocket(int socket)
        {
            switch (socket)
            {
                case 5: return getSocket5Pins();
                case 8: return getSocket8Pins();
                case 9: return getSocket9Pins();
                case 11: return getSocket11Pins();
                case 12: return getSocket12Pins();
                case 14: return getSocket14Pins();
                default: return null;
            }
        }

		//Retourne les pins du socket 5
        public static Cpu.Pin[] getSocket5Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket5.Pin4,
                GHI.Pins.FEZSpiderII.Socket5.Pin5,
                GHI.Pins.FEZSpiderII.Socket5.Pin7,
                GHI.Pins.FEZSpiderII.Socket5.Pin9,
                GHI.Pins.FEZSpiderII.Socket5.Pin6,
                GHI.Pins.FEZSpiderII.Socket5.Pin3,
                GHI.Pins.FEZSpiderII.Socket5.Pin8
            };
        }
		//Retourne les pins du socket 8
        public static Cpu.Pin[] getSocket8Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket8.Pin4,
                GHI.Pins.FEZSpiderII.Socket8.Pin5,
                GHI.Pins.FEZSpiderII.Socket8.Pin7,
                GHI.Pins.FEZSpiderII.Socket8.Pin9,
                GHI.Pins.FEZSpiderII.Socket8.Pin6,
                GHI.Pins.FEZSpiderII.Socket8.Pin3,
                GHI.Pins.FEZSpiderII.Socket8.Pin8
            };
        }
		//Retourne les pins du socket 9
        public static Cpu.Pin[] getSocket9Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket9.Pin4,
                GHI.Pins.FEZSpiderII.Socket9.Pin5,
                GHI.Pins.FEZSpiderII.Socket9.Pin7,
                GHI.Pins.FEZSpiderII.Socket9.Pin9,
                GHI.Pins.FEZSpiderII.Socket9.Pin6,
                GHI.Pins.FEZSpiderII.Socket9.Pin3,
                GHI.Pins.FEZSpiderII.Socket9.Pin8
            };
        }
		//Retourne les pins du socket 11
        public static Cpu.Pin[] getSocket11Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket11.Pin4,
                GHI.Pins.FEZSpiderII.Socket11.Pin5,
                GHI.Pins.FEZSpiderII.Socket11.Pin7,
                GHI.Pins.FEZSpiderII.Socket11.Pin9,
                GHI.Pins.FEZSpiderII.Socket11.Pin6,
                GHI.Pins.FEZSpiderII.Socket11.Pin3,
                GHI.Pins.FEZSpiderII.Socket11.Pin8
            };
        }
		//Retourne les pins du socket 12
        public static Cpu.Pin[] getSocket12Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket12.Pin4,
                GHI.Pins.FEZSpiderII.Socket12.Pin5,
                GHI.Pins.FEZSpiderII.Socket12.Pin7,
                GHI.Pins.FEZSpiderII.Socket12.Pin9,
                GHI.Pins.FEZSpiderII.Socket12.Pin6,
                GHI.Pins.FEZSpiderII.Socket12.Pin3,
                GHI.Pins.FEZSpiderII.Socket12.Pin8
            };
        }
		//Retourne les pins du socket 14
        public static Cpu.Pin[] getSocket14Pins()
        {
            return new Cpu.Pin[] {
                GHI.Pins.FEZSpiderII.Socket14.Pin4,
                GHI.Pins.FEZSpiderII.Socket14.Pin5,
                GHI.Pins.FEZSpiderII.Socket14.Pin7,
                GHI.Pins.FEZSpiderII.Socket14.Pin9,
                GHI.Pins.FEZSpiderII.Socket14.Pin6,
                GHI.Pins.FEZSpiderII.Socket14.Pin3,
                GHI.Pins.FEZSpiderII.Socket14.Pin8
            };
        }
    }
}
