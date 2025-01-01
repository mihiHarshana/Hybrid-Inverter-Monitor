using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net;
using InverterMon.Shared.Models;

namespace InverterMon.Server.InverterService.Commands;

public class GetStatus : Command<InverterStatus>
{
    public override string CommandString { get; set; } = "QPIGS";
    Boolean GridLow = false;
    Boolean GridOut = false;
    Boolean pvUP = false;
    Boolean systemUp = false;

    public override void Parse(string responseFromInverter)
    {
        //(232.0 50.1 232.0 50.1 0000 0000 000 476 27.02 000 100 0553 0000 000.0 27.00 00000 10011101 03 04 00000 101a\xc8\r
        //(000.0 00.0 229.8 50.0 0851 0701 023 355 26.20 000 050 0041 00.0 058.5 00.00 00031 00010000 00 00 00000 010 0 01 0000

        if (responseFromInverter.StartsWith("(NAK"))
            return;

        var parts = responseFromInverter[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        char[]? dstatus = parts[16].ToCharArray();

        Result.GridVoltage = decimal.Parse(parts[0]);
        Result.OutputVoltage = decimal.Parse(parts[2]);
        Result.LoadWatts = int.Parse(parts[5]);
        Result.LoadPercentage = decimal.Parse(parts[6]);
        Result.BatteryVoltage = decimal.Parse(parts[8]);
        Result.BatteryChargeCurrent = int.Parse(parts[9]);
        Result.HeatSinkTemperature = int.Parse(parts[11]);
        Result.PVInputCurrent = decimal.Parse(parts[12]);
        Result.PVInputVoltage = decimal.Parse(parts[13]);
        Result.BatteryDischargeCurrent = int.Parse(parts[15]);
        Result.PVInputWatt = Result.PVInputVoltage == 00 ? 0 : Convert.ToInt32(int.Parse(parts[19]));

        if (dstatus != null)
        {
            if (dstatus[5].ToString().Equals("1") && dstatus[6].ToString().Equals("1") && dstatus[7].ToString().Equals("0"))
            {
                Result.DSChargeStatus = "Charging with Solar charger ";
            }
            else if (dstatus[5].ToString().Equals("1") && dstatus[6].ToString().Equals("0") && dstatus[7].ToString().Equals("1"))
            {
                Result.DSChargeStatus = "Charging with Grid charger";
            }

            else if (dstatus[5].ToString().Equals("1") && dstatus[6].ToString().Equals("1") && dstatus[7].ToString().Equals("1"))
            {
                Result.DSChargeStatus = "Charging  with Solar and Grid charger";
            }
            else
            {
                Result.DSChargeStatus = "Charging off";
            }
        }

        if (Result.GridVoltage >= 245)
        {
            using StreamWriter file = new("powerHistory.txt", append: true);
            file.WriteLine(DateTime.Now + " " + parts[0]);
            file.Close();
        }
        else if (Result.GridVoltage <= 100 && GridLow == false)
        {
            using StreamWriter file = new("powerHistory.txt", append: true);
            file.WriteLine(DateTime.Now + " Low voltage " + parts[0]);
            sendEmail("Invertor Monitor | Low Voltage ", "Power Outage at " + parts[0] + " at " + DateTime.Now);
            GridLow = true;
            file.Close();
        }

        else if (Result.GridVoltage <= decimal.Parse("0.0") && GridOut == false)
        {
            using StreamWriter file = new("powerHistory.txt", append: true);
            file.WriteLine(DateTime.Now + " Power outage  at " + parts[0]);
            sendEmail("Invertor Monitor | Power Outage ", "Power Outage at " + parts[0] + " at " + DateTime.Now);
            GridOut = true;
            file.Close();
        }

        else if (Result.GridVoltage >= 230 && Result.GridVoltage <= 245 && GridLow == true)
        {
            using StreamWriter file = new("powerHistory.txt", append: true);
            file.WriteLine(DateTime.Now + " Power restored at  " + parts[0]);
            sendEmail("Invertor Monitor | Power Restored", "Power Restored at " + parts[0] + " at " + DateTime.Now);

            GridLow = false;
            file.Close();
        }

        if (Result.PVInputWatt <= 1 && Result.PVInputVoltage <= 0 && pvUP == false)
        {
            using StreamWriter file = new("pvhistory.txt", append: true);
            file.WriteLine(DateTime.Now + " PV Ends  " + parts[13]);
            pvUP = true;
            file.Close();
        }
        else if (Result.PVInputWatt > 1 && Result.PVInputVoltage >= 120 && pvUP)
        {
            using StreamWriter file = new("pvhistory.txt", append: true);
            file.WriteLine(DateTime.Now + " PV Starts  " + parts[13]);
            pvUP = false;
            file.Close();
        }
        if (systemUp == false)
        {
            using StreamWriter file = new("powerHistory.txt", append: true);
            file.WriteLine(DateTime.Now + " System started up  ");
            sendEmail("Invertor Monitor | System Started", "System Started Up");

            systemUp = true;
            file.Close();
        }

        if (Result.GridVoltage >= 230 && Result.GridVoltage <= 245 && GridLow == true && GridOut == true)
        {
            GridLow = false;
            GridOut = false;
        }
    }


    public void sendEmail(String strSubject, String messageBody)
    {
        string certificatePath = @"/home/mihindu/certificate.pfx";
        string certificatePassword = "MyTestKeyForSendingEmail";

        string smtpServer = "smtp.gmail.com"; // Use Gmail SMTP server
        int smtpPort = 587; // 587; // For TLS, use port 587
        string smtpUser = ""; // Your email address
        string smtpPass = "";

        // Email message configuration
        string fromEmail = "@gmail.com";
        string toEmail = "@gmail.com";
        string subject = strSubject;
        string body = messageBody;

        try
        {
            // Create the email message
            MailMessage mail = new MailMessage(fromEmail, toEmail, subject, body);

            // Configure the SMTP client
            SmtpClient smtpClient = new SmtpClient(smtpServer)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true, // Enable SSL
            };

            // Send the email
            smtpClient.Send(mail);

            Console.WriteLine("Email sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending email: " + ex.Message);
        }

    }

}