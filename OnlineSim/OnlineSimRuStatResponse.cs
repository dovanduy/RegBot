﻿namespace OnlineSimRu
{
    public class OnlineSimRuStatResponse
    {
        public string name { get; set; }
        public int position { get; set; }
        public int code { get; set; }
        public bool other { get; set; }
        public bool _new { get; set; }
        public bool enabled { get; set; }
        public OnlineSimRuStatResponseServices services { get; set; }
    }

    public class OnlineSimRuStatResponseServices
    {
        public OnlineSimRuStatResponseServiceMailru mailru { get; set; }
        public OnlineSimRuStatResponseServiceGoogle google { get; set; }
        public OnlineSimRuStatResponseServiceYandex yandex { get; set; }
    }

    public class OnlineSimRuStatResponseServiceMailru
    {
        public int? count { get; set; }
        public double price { get; set; }
    }

    public class OnlineSimRuStatResponseServiceGoogle
    {
        public int? count { get; set; }
        public double price { get; set; }
    }

    public class OnlineSimRuStatResponseServiceYandex
    {
        public int? count { get; set; }
        public double price { get; set; }
    }

}