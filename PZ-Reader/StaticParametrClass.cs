using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PZ_Reader
{
    static class StaticParametrClass
    {
        //Успешная авторизация
        public static bool AutorisationPZ = false;

        //Параметр регулирующий работы, если истина то идет выполнение какой то команды
        public static bool CommandJob;

        //Название выполняющейся команды
        public static string CommandName;

        //Ожидать последнее сообщение и прекратить выдачу пакетов измерителем
        public static bool LastRead = false;

        //Флаг прихода ответа об успешной записи новыхзначений, можно опрашивать
        public static bool WriteNewParametrOk;

        //Массив установок прибора
        public static object[] PriborParametrSetups = new object[12];

        //Поправочный коэффициент, значение записанное в 3 -х байтовое слово
        //Вырванное непосредственно из прибора
        public static byte[] PoprKoeficient;

        public static string NewCommand = "";
    }
}
