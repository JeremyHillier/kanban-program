// Test classes run one at a time. TempFolder.Dispose clears every pooled SQLite connection in the process, so a
// database test in another class running at the same moment could lose its connection mid-query (seen as an
// occasional "disposed object" error in BrandNewTaskFile_StartsWithTheDefaultBoard). Most classes already share
// the one WPF collection, which runs serially, so this costs little.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
