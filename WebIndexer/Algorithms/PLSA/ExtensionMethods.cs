using System.Collections.Generic;
using HiddenMarkov.Algorithms.PLSA.Model;
using WebIndexer.Algorithms.PLSA.Model;
using WebIndexer.Collections;

namespace WebIndexer.Algorithms.PLSA
{
    static class ExtensionMethods
    {
        public static double MatrixSumByRow(this MatrixHashTable<Term, Topic, double> value, Term term, List<Topic> topics)
        {
            //
            // Uppercase the first letter in the string.
            //
            /*   if (value.GetLength() > 0)
               {
                   char[] array = value.ToCharArray();
                   array[0] = char.ToUpper(array[0]);
                   return new string(array);
               }*/
            double result = 0.0;
            foreach (var topic in topics)
            {
                result += value[term, topic];
            }
            return result;
        }
    }
}