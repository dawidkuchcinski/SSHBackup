using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace SSHBackup2
{
    class Program
    {
        static void Main()
        {
            string nazwa = DateTime.Now.ToString("yyyy-MM-dd");
            string now = DateTime.Now.ToString();
            string path = @"Path\to\store\log_file" + nazwa + "-log.txt";

            ConnectToSSH();
            File.AppendAllText(path, now + " " + "----------------------------KONIEC_PROGRAMU----------------------------" + Environment.NewLine);

            void ConnectToSSH()
            {
                try
                {
                    const string SSH_USR = "ssh_username";
                    string SSH_HST = "host_ip_addres";
                    string key = File.ReadAllText(@"path\to\RSA_key");

                    Regex removeSubjectRegex = new Regex("Subject:.*[\r\n]+", RegexOptions.IgnoreCase);
                    key = removeSubjectRegex.Replace(key, "");
                    MemoryStream buf = new MemoryStream(Encoding.UTF8.GetBytes(key));


                    var keyboardInteractiveAuth = new KeyboardInteractiveAuthenticationMethod(SSH_USR);
                    keyboardInteractiveAuth.AuthenticationPrompt += (sender, args) =>
                    {
                        foreach (var authenticationPrompt in args.Prompts)
                            authenticationPrompt.Response = "ssh_password";
                    };

                    var privateKeyAuth = new PrivateKeyAuthenticationMethod(SSH_USR,
                        new PrivateKeyFile(buf));

                    var connectionInfo = new ConnectionInfo(SSH_HST, 22, SSH_USR, privateKeyAuth, keyboardInteractiveAuth);


                    using (var sshClient = new SshClient(connectionInfo))
                    {
                        try
                        {
                            sshClient.Connect();
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(path, now + " " + ex.Message + Environment.NewLine);
                            //Environment.Exit(0);
                        }
                        File.AppendAllText(path, now + " " + "Połączono" + Environment.NewLine);
                        var prompRegex = new Regex(@"[$#>]");
                        var modes = new Dictionary<Renci.SshNet.Common.TerminalModes, uint>();
                        using (ShellStream shellStream = sshClient.CreateShellStream("xterm", 255, 50, 800, 600, 1024, modes))
                        {
                            shellStream.Write("mysqldump -uroot -p NAME > ~/" + nazwa + ".sql\n");
                            File.AppendAllText(path, now + " " + "mysqldump wpisany" + Environment.NewLine);
                            shellStream.Expect("password");
                            shellStream.Write("SSH_PASSWORD\n");
                            File.AppendAllText(path, now + " " + "Poświadczono" + Environment.NewLine);
                            shellStream.Write("SSH_PASSWORD\n");

                            CopyFile(nazwa, connectionInfo);

                            shellStream.Write("rm " + nazwa + ".sql\n");
                            File.AppendAllText(path, now + " " + "Usunięto dumpa" + Environment.NewLine);
                            var output = shellStream.Expect(prompRegex);
                        }

                        sshClient.Disconnect();
                        File.AppendAllText(path, now + " " + "Wylogowano" + Environment.NewLine);
                    }
                }
                catch(Exception ex)
                {
                    File.AppendAllText(path, now + " " + ex.Message + Environment.NewLine);
                }
                
            }

            void CopyFile(string nazwaPliku, ConnectionInfo connectionInfo)
            {
                try
                {
                    string localFileName = System.IO.Path.GetFileName(@nazwaPliku);
                    string remoteDirectory = "/root/";
                    string localDirectory = @"Path\to\folder\where\I\store\dumps";

                    using (var sftpClient = new SftpClient(connectionInfo))
                    {
                        sftpClient.Connect();
                        var files = sftpClient.ListDirectory(remoteDirectory);
                        foreach (var file in files)
                        {
                            string remoteFileName = file.Name;
                            if ((!file.Name.StartsWith(".")) && (file.LastWriteTime.Date == DateTime.Today))
                            {
                                using (Stream file1 = File.OpenWrite(localDirectory + remoteFileName))
                                {
                                    sftpClient.DownloadFile(remoteDirectory + remoteFileName, file1);
                                }
                            }
                        }
                        sftpClient.Disconnect();
                    }
                }
                catch(Exception ex)
                {
                    File.AppendAllText(path, now + " " + ex.Message + Environment.NewLine);
                }
            }
        }
    }
}
