using System;
using System.Collections.Generic;
using System.Linq;
using Plexus;

namespace CSharp.Sample
{
    public class Message
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }

    class Program
    {        

        public class MyActor : Actor<Message>
        {
            public override void Handle(Message value)
            {
                Console.WriteLine(value.Text);
            }

            public override void Handle(Message value, Action<object> result)
            {
                Console.WriteLine(value.Text);
                result("Received: "+value.Text);
            }
        }

        static void Main(string[] args)
        {
        }


    }
}
