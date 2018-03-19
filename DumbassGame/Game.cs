using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Game
{
    static string deckname = "basic";
    static bool host = false;
    static int mode = 0; //0=start 1=connected 2=hash ok 3=waiting for input 4=waiting for result 9 = ded
    static Dictionary<string, int[]> deck;
    static int hp = 10, enemyHp = 10;
    static List<string> remainingCards;
    static string sendcard, reccard;
    static int port;
    static int deckHash;
    static TcpListener listener;
    static TcpClient client;
    static IPAddress addr = IPAddress.Any;
    static NetworkStream stream;
    static bool restartMe, RestartOther;
    static void Main(string[] args)
    {
        try
        {
            LoadGameData();
            MainLoop();
        }
        catch (Exception e)
        {
            WriteToConsole("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this!");
        }
    }

    static void LoadGameData()
    {
        string[] cfg = ReadFile("config.cfg").Split();
        port = int.Parse(cfg[0]);
        LoadDeck();
    }



    static void MainLoop()
    {
        while (true)
        {
            string consoleInput = ReadFromConsole();
            if (string.IsNullOrWhiteSpace(consoleInput)) continue;
            Interpret(consoleInput);
        }
    }

    static void PrepareRound()
    {
        restartMe = false;
        RestartOther = false;
        remainingCards = new List<string>(deck.Keys);
        enemyHp = 10;
        hp = 10;
        mode = 3;
        WriteToConsole("Restartet");
        WriteToConsole("THE DUEL HAS BEGUN!");


    }

    static void LoadDeck()
    {
        if (mode != 0)
            return;
        string d = ReadFile(deckname + ".deck");
        string[] deckFileLines = d.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        deck = new Dictionary<string, int[]>();
        foreach (string s in deckFileLines)
        {
            string[] sa = s.Split('#');
            deck.Add(sa[0].ToLower(), new int[] { int.Parse(sa[1]), int.Parse(sa[2]) });
        }
        deckHash = d.GetHashCode();
        remainingCards = new List<string>(deck.Keys);
        WriteToConsole("Loaded deck " + deckname);
    }

    static void Interpret(string input)
    {
        input = input.ToLower();
        string[] inputDiv = input.Split(' ');
        if (mode != 0 && input == "disconnect")
        {
            EndConnection();
        }
        else if (mode == 0 && input == "host")
        {
            Host();
        }
        else if (input == "restart")
        {
            restartMe = true;
            if (restartMe && RestartOther)
                PrepareRound();
            else
                SendData("restart");
        }
        else if (mode == 0 && inputDiv[0].ToLower() == "connect")
        {
            Connect(inputDiv[1]);
        }
        else if (mode == 0 && inputDiv[0].ToLower() == "deck")
        {
            deckname = inputDiv[1];
            LoadDeck();
        }
        else if (mode != 0 && inputDiv[0] == "say")
        {
            string s = "say#";
            for (int i = 1; i < inputDiv.Length; i++)
            {
                s += inputDiv[i];
            }
            SendData(s);
        }
        else if (mode == 3 && remainingCards.Contains(input) && sendcard == null)
        {
            SendData("card#" + input);
            sendcard = input;
            remainingCards.Remove(input);
            WriteToConsole("Spell cast!");
            if (reccard != null && sendcard != null)
            {
                CalcWin();
            }
        }
        else if (mode == 3 && remainingCards.Contains(input) && sendcard != null)
        {
            WriteToConsole("You already casted a spell");
        }
        else if (input == "spells")
        {
            if (remainingCards.Count > 0)
            {
                string s = remainingCards[0] + "[" + deck[remainingCards[0]][0] + "|" + deck[remainingCards[0]][1] + "]";
                for (int i = 1; i < remainingCards.Count; i++)
                {
                    s += ", " + remainingCards[i] + "[" + deck[remainingCards[i]][0] + "|" + deck[remainingCards[i]][1] + "]";
                }
                WriteToConsole(s);
            }
            else
            {
                WriteToConsole("No spells remaining");
            }

        }
        else if (input == "hp")
        {
            WriteToConsole(hp + "");
        }
        else if (input == "help")
        {
            WriteToConsole("host - host a game on your pc");
            WriteToConsole("connect [ip] - connect to the ip");
            WriteToConsole("spells - show all your remaining spells");
            WriteToConsole("hp - show your remaining hp");
            WriteToConsole("[spell] - cast the selected spell");
            WriteToConsole("disconnect - end the connection");
            WriteToConsole("deck [.deck file]- load a different deck");
            WriteToConsole("restart - restart the game");
        }
        else if (mode == 3 && !remainingCards.Contains(input))
        {
            WriteToConsole("You dont have this spell");
        }
        else
        {
            WriteToConsole("Wrong input");
        }
    }


    static void WriteToConsole(string message = "")
    {
        Console.WriteLine(message);
    }

    static string ReadFromConsole()
    {
        return Console.ReadLine();
    }

    static string ReadFile(string path)
    {
        using (StreamReader sr = new StreamReader(path))
        {
            String s = sr.ReadToEnd();
            return s;
        }
    }

    static async void Connect(string ip)
    {
        client = new TcpClient();
        WriteToConsole("Connecting...");

        Task t = Task.Run(() =>
        {
            Task task = client.ConnectAsync(IPAddress.Parse(ip), port);
            while (!client.Connected)
            {

            }
        });
        if (await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(10))) == t)
        {
            WriteToConsole("Connected!");
            stream = client.GetStream();
            StreamReading();
            SendData("" + deckHash);
            mode = 1;
        }
        else
        {
            WriteToConsole("Couldnt connect");
        }
    }
    static async void Host()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        WriteToConsole("Waiting for player...");
        client = await listener.AcceptTcpClientAsync();
        WriteToConsole("A player has connected!");
        stream = client.GetStream();
        host = true;
        StreamReading();
        SendData("" + deckHash);
        mode = 1;
    }
    static void SendData(string message)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }
    static async void StreamReading()
    {
        while (true)
        {
            byte[] buffer = new byte[256];
            int l = await stream.ReadAsync(buffer, 0, buffer.Length);
            string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, l);
            string[] sDiv = msg.Split('#');
            if (mode == 1)
            {
                if (msg == "" + deckHash)
                {
                    WriteToConsole("Deck is the same");
                    mode = 3;
                    WriteToConsole("THE DUEL HAS BEGUN!");

                }
                else
                {
                    WriteToConsole("Looks like the other player has a different deck");
                }
            }
            if (mode != 0 && sDiv[0] == "say")
            {
                WriteToConsole(sDiv[1]);
            }
            if (mode != 0 && sDiv[0] == "card")
            {
                reccard = sDiv[1];
                if (reccard != null && sendcard != null)
                {
                    CalcWin();
                }
            }
            if (msg == "restart")
            {
                RestartOther = true;
                if (restartMe && RestartOther)
                    PrepareRound();
                else
                    WriteToConsole("The other player wants to restart, type restart to restart");

            }
        }
    }
    static void CalcWin()
    {
        int loss = Math.Max(deck[reccard][0] - deck[sendcard][1], 0); // I | Ii | II | I_
        hp -= loss;
        int eloss = Math.Max(deck[reccard][1] - deck[sendcard][0], 0);
        enemyHp -= eloss;
        WriteToConsole("You used " + sendcard + ", your enemy lost " + eloss + " HP");
        WriteToConsole("Your enemy used " + reccard + ", you lost " + loss + " HP");
        reccard = null;
        sendcard = null;
        if (hp <= 0 && enemyHp <= 0)
        {
            WriteToConsole("You are both dead, type restart to restart");
            mode = 9;
        }
        else if (hp <= 0)
        {
            WriteToConsole("You are dead, type restart to restart");
            mode = 9;
        }
        else if (enemyHp <= 0)
        {
            WriteToConsole("Your enemy is dead, type restart to restart");
            mode = 9;
        }
    }

    static void EndConnection()
    {
        client.Dispose();
        stream.Close();
        mode = 0;
    }
}
