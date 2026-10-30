using Xunit;

// Тесты мутируют статический мир стабов (MapBox.instance) — параллелизм отключаем.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
