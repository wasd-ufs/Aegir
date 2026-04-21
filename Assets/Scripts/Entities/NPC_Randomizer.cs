using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NPCsData))]
public class NPC_Randomizer : MonoBehaviour
{
    public float minVida, maxVida, minForça, maxForça, minCusto, maxCusto;

    public bool randomizarClasse, randomizarNome;
    private NPCsData nPCs;
    
    private string[] nomesIniciais = {
        "Jack", "Anne", "Edward", "Will", "Elizabeth", "Henry",
        "Charles", "Mary", "Samuel", "Thomas", "Francis", "Grace",
        "Robert", "James", "Calico", "Benjamin", "Bartholomew", "John",
        "William", "Diego", "Isabella", "Rodrigo", "Morgan", "Clara",
        "Evan", "Felix", "Hugo", "Iris", "Jonas", "Katarina",
        "Leon", "Marta", "Nathan", "Olivia", "Pedro", "Quinn",
        "Rafael", "Scarlett", "Tobias", "Uma", "Victor", "Willa",
        "Xavier", "Yara", "Zane", "Adelaide", "Brutus", "Cordelia",
        "Dorian", "Elena", "Finn", "Greta", "Hector", "Ida",
        "Alaric", "Beatrix", "Caspian", "Delilah", "Edmund", "Fatima",
        "Godwin", "Harriet", "Ignacio", "Jezebel", "Killian", "Lorena",
        "Magnus", "Nadia", "Orion", "Petra", "Quincy", "Rowena",
        "Silas", "Tamara", "Ulric", "Valentina", "Warren", "Xena",
        "Yorick", "Zelda", "Aldric", "Brigida", "Cedric", "Dalia",
        "Enzo", "Freya", "Gareth", "Helena", "Ivan", "Joana",
        "Kira", "Lucian", "Mirela", "Nero", "Odessa", "Percival",
        "Rosamund", "Soren", "Tristan", "Ursula", "Vasco", "Wendy"
    };

    private string[] nomesFinais = {
        "Barbosa", "Sparrow", "o Ruivo", "Enganador", "Tempestade", "Corvino",
        "Maos de Ferro", "a Sanguinaria", "o Maldito", "das Sombras", "Ossos",
        "o Sem-Lei", "Perna de Pau", "Olho de Vidro", "o Impiedoso", "Mare Negra",
        "Cranio", "o Terrivel", "das Profundezas", "Dente de Ouro", "o Fantasma",
        "Sete Mares", "o Amaldicoado", "Garras", "o Cruel", "Alma Perdida",
        "Cao Selvagem", "o Lendario", "do Abismo", "Polvora", "o Esquecido",
        "Sangue Frio", "No Cego", "o Implacavel", "Veneno", "o Infame",
        "Tempestade Negra", "Morte Certa", "o Devorador", "Corvo", "o Obscuro",
        "Ancora Torta", "o Perseguido", "Faca Torta", "o Lobo", "Sal e Polvora",
        "o Renegado", "Bruma Negra", "o Naufrago", "o Cacador", "Cobre Enferrujado",
        "o Despietado", "Vento Podre", "o Abutre", "Madrugada Sangrenta", "o Infernal",
        "Dentes Negros", "a Maldita", "o Solitario", "Cheiro de Rum", "o Espectro",
        "Carne Seca", "o Corsario", "Lamina Fria", "o Indomavel", "Fumaca Negra",
        "o Degolador", "Agua Turva", "a Temida", "Coracao de Pedra", "o Brutal",
        "Meia Noite", "o Profano", "Rajada de Morte", "o Inclemente", "Farrapos",
        "o Azarado", "Peixe Podre", "a Feroz", "Onda Negra", "o Desaparecido",
        "Chumbo Grosso", "o Traidor", "Barba de Aco", "o Sombrio", "Espinha de Peixe",
        "o Miseravel", "Velas Negras", "a Impiedosa", "Cano Curto", "o Execrado"
    };

    void Awake()
    {
        nPCs = GetComponent<NPCsData>();
        nPCs.vidaMáxima = UnityEngine.Random.Range(minVida, maxVida);
        nPCs.força = UnityEngine.Random.Range(minForça, maxForça);
        nPCs.custo = UnityEngine.Random.Range(minCusto, maxCusto);
        nPCs.Heal(nPCs.vidaMáxima);

        if (randomizarNome)
        {
            nPCs.NPC_Name = nomesIniciais[UnityEngine.Random.Range(0, nomesIniciais.Length)] + " " + nomesFinais[UnityEngine.Random.Range(0, nomesFinais.Length)];
        }

        if (randomizarClasse)
        {
            Array valoresClasse = Enum.GetValues(typeof(NPCsData.Class));
            List<NPCsData.Class> classes = new();
            foreach (Enum value in valoresClasse)
            {
                if ((NPCsData.Class) value != NPCsData.Class.Barco)
                    classes.Add((NPCsData.Class)value);
            }
            nPCs.creatureClass = classes[UnityEngine.Random.Range(0, classes.Count)];
        }
    }
}
