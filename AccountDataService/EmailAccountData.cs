﻿using System;

namespace AccountData.Service
{
    public class EmailAccountData : IAccountData
    {
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public DateTime BirthDate { get; set; }
        public SexEnum Sex { get; set; }
        public string AccountName { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string PhoneCountryCode { get; set; }
        public string Phone { get; set; }
        public bool Success { get; set; }
        public string ErrMsg { get; set; }
    }
}