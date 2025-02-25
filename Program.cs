using System.Diagnostics;
using System.Text.Json;
using Script.Bypasses;
using Script.Util;

Debug.WriteLine("\tFUCK\tYEET");

string data = MethodEnhance.JsonSerialize(new
{
    Fuck = "dsfsdf       dsfsd   sdfsd",
    Yeet = new {
        fuck = 123,
        dotnet = '2'
    },
    Pi = Math.PI
});

Debug.WriteLine(data);

while (true) Console.ReadLine();

//riisdevencryptaes
//usualkey
//U2FsdGVkX1/HG0WgERq2EdbPxNHq9SZIJDxooIOYBZ2IEGMDO0TMXXoTuTnmZsV+HGayphKtZ6tYhbetqsUliQ==