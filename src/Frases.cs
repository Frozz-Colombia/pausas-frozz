// Frases motivacionales que Frozzy dice al completar una pausa.
// "{n}" se reemplaza por ", <nombre>" si la persona aceptó que la llame por su nombre,
// o se elimina si no. Se reparten como una baraja: no se repite ninguna hasta usarlas todas.
using System;
using System.Collections.Generic;

namespace PausasFrozz
{
    static class Frases
    {
        static readonly string[] Motivacion = {
            "¡Así se hace{n}! Ahora sí, a seguir con toda.",
            "¡Recargado{n}! Vamos por esa siguiente tarea.",
            "Cuerpo descansado, mente enfocada. ¡A darle{n}!",
            "¡Eso{n}! Un minuto para ti también es trabajo bien hecho.",
            "Tus ojos te lo agradecen{n}. ¡Sigamos!",
            "¡Firme{n}! Con energía nueva para lo que viene.",
            "Pequeñas pausas, grandes resultados. ¡Vamos!",
            "¡Listo{n}! Ahora a enfriar esos pendientes.",
            "Cuidarte también es ser profesional. ¡Bien ahí!",
            "¡Qué disciplina{n}! Así da gusto trabajar.",
            "Respira, sonríe y a seguir. ¡Tú puedes{n}!",
            "¡Pausa cumplida! Ahora, una tarea a la vez.",
            "Mente fresca como un buen chiller. ¡A trabajar{n}!",
            "¡Bien hecho{n}! Tu espalda acaba de sonreír.",
            "El que se cuida, rinde más. ¡Sigamos con todo!",
            "¡Un paso más hacia un día sin dolores{n}!",
            "¡Recarga completa al 100 %! A seguir{n}.",
            "Lo importante no es correr, es llegar bien. ¡Vamos!",
            "¡Excelente{n}! Ahora enfoca esa energía en lo que sigue.",
            "Gracias por cuidarte{n}. ¡El equipo te necesita bien!",
            "¡Así me gusta! Constancia es la clave.",
            "Hidratado, estirado y listo. ¡A romperla{n}!",
            "¡Bien{n}! Cada pausa suma a tu salud.",
            "Un cuerpo en movimiento es una mente en movimiento. ¡Dale!",
            "¡Eres un crack{n}! Sigamos sumando.",
            "Ya cumpliste con tu salud; ahora cumple con tus metas. ¡Vamos!",
            "¡Qué buena energía{n}! Contagia al equipo.",
            "Frío por fuera, motivado por dentro. ¡A seguir!",
            "¡Lo lograste{n}! Ahora, a terminar lo que empezaste.",
            "Tu yo del futuro te lo agradece. ¡Bien hecho!",
            "¡Pausa hecha, día mejorado{n}!",
            "Concentración renovada. ¡Hora de brillar{n}!",
            "¡Seguimos{n}! Tu bienestar también es prioridad.",
            "Trabaja seguro, cuida tu salud. ¡Tú lo haces muy bien{n}!",
            "¡Bien ahí! Un puesto bien ajustado y un cuerpo descansado.",
            "No hay prisa que valga tu salud. ¡A seguir con calma y foco!",
            "¡Bacano{n}! Volvemos con toda.",
            "Mientras más te cuidas, mejor trabajas. ¡Sigamos!",
            "¡Cumpliste{n}! Te ganaste una sonrisa.",
            "Descansar también es avanzar. ¡A darle!",
            "¡Qué berraquera{n}! Así se trabaja.",
            "Ojos descansados ven mejores soluciones. ¡Vamos!",
            "¡Uno más para la cuenta{n}! Sigue así.",
            "Ese estiramiento vale oro. ¡Ahora a producir{n}!",
            "¡Con toda la actitud{n}! El resto del día es tuyo.",
            "Tu salud es el mejor proyecto. ¡Y va muy bien!",
            "¡Volvemos a la carga, pero sin cargar la espalda{n}!",
            "¡Te quedó perfecto{n}! A seguir rindiendo.",
            "Paso a paso se llega lejos. ¡Vamos{n}!",
            "¡Bien hidratado y bien enfocado{n}!",
            "La mejor herramienta de trabajo eres tú. ¡Cuídala!",
            "¡Eso es compromiso{n}! Sigamos.",
            "Pausa activa, mente activa. ¡A trabajar!",
            "¡Le diste duro{n}! Ahora a cerrar pendientes.",
            "Hoy es un gran día para cuidarte. ¡Y lo estás haciendo{n}!",
            "¡De una{n}! Seguimos con toda la energía.",
            "Menos tensión, más concentración. ¡Vamos!",
            "¡Qué orgullo{n}! Sigue cuidándote así.",
            "Recuerda: postura derecha, mente clara. ¡A seguir!",
            "¡Bien jugado{n}! Tu cuerpo ganó este round.",
            "Un trabajador sano es un trabajador feliz. ¡Firme!",
            "¡Arriba ese ánimo{n}! Lo mejor del día está por venir.",
            "Ya estás listo para lo que venga. ¡Vamos{n}!",
            "¡Así como un buen equipo de frío: estable y eficiente{n}!",
            "¡Gracias por hacer la pausa{n}! Ahora sí, a darle.",
            "Cada pausa cuenta. ¡Y hoy llevas varias!",
            "¡Ánimo{n}! Ya casi terminas la jornada.",
            "¡Fresquito y con energía{n}! Sigamos.",
        };

        static readonly List<int> bolsa = new List<int>();
        static readonly Random rnd = new Random();

        public static string Siguiente(string nombre)
        {
            if (bolsa.Count == 0)
            {
                for (int i = 0; i < Motivacion.Length; i++) bolsa.Add(i);
                for (int i = bolsa.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = bolsa[i]; bolsa[i] = bolsa[j]; bolsa[j] = t; }
            }
            int k = bolsa[bolsa.Count - 1];
            bolsa.RemoveAt(bolsa.Count - 1);
            return Personalizar(Motivacion[k], nombre);
        }

        public static string Personalizar(string s, string nombre)
        {
            return s.Replace("{n}", string.IsNullOrEmpty(nombre) ? "" : ", " + nombre);
        }
    }
}
