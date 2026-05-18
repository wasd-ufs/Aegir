using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Realiza a geração procedural inicial de um NPC/Inimigo que acaba de ser instanciado no mundo,
/// definindo seus limites de vida, força, custo baseados nos parâmetros estipulados e sorteando seu nome e classe.
/// </summary>
[RequireComponent(typeof(NPCsData))]
public class NPCRandomizer : MonoBehaviour
{
    #region Configurações de Randomização
    [Header("Limites de Atributos")]
    [SerializeField] private float _minHealth;
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _minStrength;
    [SerializeField] private float _maxStrength;
    [SerializeField] private float _minCost;
    [SerializeField] private float _maxCost;

    [Header("Opções de Sorteio")]
    [SerializeField] private bool _shouldRandomizeClass;
    [SerializeField] private bool _shouldRandomizeName;

    private NPCsData _npcsData;
    #endregion

    #region Listas de Nomes
    private string[] _firstNamesArray = {
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

    private string[] _lastNamesArray = {
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
    #endregion

    #region Inicialização
    private void Awake()
    {
        _npcsData = GetComponent<NPCsData>();

        // Sorteia Atributos Básicos
        _npcsData.MaxHealth = UnityEngine.Random.Range(_minHealth, _maxHealth);
        _npcsData.Strength = UnityEngine.Random.Range(_minStrength, _maxStrength);
        _npcsData.Cost = UnityEngine.Random.Range(_minCost, _maxCost);

        _npcsData.Heal(_npcsData.MaxHealth);

        // Define Títulos
        if (_shouldRandomizeName)
        {
            string firstName = _firstNamesArray[
                UnityEngine.Random.Range(0, _firstNamesArray.Length)
            ];

            string lastName = _lastNamesArray[
                UnityEngine.Random.Range(0, _lastNamesArray.Length)
            ];

            _npcsData.NpcName = firstName + " " + lastName;
        }

        // Define a ocupação do NPC para combate excluindo a entidade "Barco" da roleta
        if (_shouldRandomizeClass)
        {
            Array classValues = Enum.GetValues(typeof(NPCsData.Class));

            List<NPCsData.Class> availableClassesList = new();

            foreach (Enum classValue in classValues)
            {
                if ((NPCsData.Class)classValue != NPCsData.Class.Ship)
                {
                    availableClassesList.Add((NPCsData.Class)classValue);
                }
            }

            _npcsData.CreatureClass = availableClassesList[
                UnityEngine.Random.Range(0, availableClassesList.Count)
            ];
        }
    }
    #endregion
}