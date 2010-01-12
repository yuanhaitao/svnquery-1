﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SvnQuery
{
    /// <summary>
    /// Simple credentials obfuscation.
    /// </summary>
    public class Credentials
    {
        public Credentials()
        {}

        public Credentials(string data)
        {
            byte[] credentials = Convert.FromBase64String(data);
            byte[] user = new byte[credentials[0] - 32];
            byte[] password = new byte[credentials[2] - 32];

            int i = 4;
            for (int n = 0; n < user.Length; ++n, i += 2)
            {
                user[n] = (byte)(credentials[i] ^ credentials[i - 1]);
            }
            User = Encoding.UTF8.GetString(user);
            for (int n = 0; n < password.Length; ++n, i += 2)
            {
                password[n] = (byte)(credentials[i] ^ credentials[i - 3]);
            }
            Password = Encoding.UTF8.GetString(password);
        }

        public string User = "";

        public string Password = "";

        public override string ToString()
        {
            byte[] user = Encoding.UTF8.GetBytes(User);
            byte[] password = Encoding.UTF8.GetBytes(Password);
            int len = (user.Length + password.Length + 2)*2;
            byte[] credentials = new byte[73];
            new Random(user.GetHashCode() ^ password.GetHashCode()).NextBytes(credentials);
            if (len > credentials.Length) return "";

            credentials[0] = (byte) (user.Length + 32);
            credentials[2] = (byte) (password.Length + 32);

            int i = 4;
            foreach (byte b in user)
            {
                if (i < credentials.Length) credentials[i] = (byte)(b ^ credentials[i - 1]);
                i += 2;
            }
            foreach (byte b in password)
            {
                if (i < credentials.Length) credentials[i] = (byte)(b ^ credentials[i - 3]);
                i += 2;
            }
            return Convert.ToBase64String(credentials);
        }
    }
}
