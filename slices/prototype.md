# Slices Prototype Plan

This document describes the changes we will be making to .NET in order to prototype array slices.  This is a feature that affects virtually every layer of our ecosystem: runtime, codegen, language, GC etc ...  

The goals of the prototype are to implement enough of the feature to measure the performance impact of the change and evaluate the impact on the code base.  In particular we wan to measure the extra allocations for arrays and the extra indirection for element access.  These were the more prominent cocnerns that came up that cannot be eliminated without actually measuring the change.  

## Slice Feature

### User Experience 

Slices allow us to view the primary components of arrays as separate pieces: the bounds and the storage.  This means it's possible to create multiple views (slices) of the same array without having to fully copy the storage.

``` csharp
var array = new[] { 1, 2, 3, 4}; 
var slice = array.Slice(index:1, length: 2);
array[1] = 13;
Console.WriteLine(slice[0]);        // Prints: 13 
Console.WriteLine(slice.Length);    // Prints: 2
```

In addition the langauge will adapt to support convenient syntax for creating slices.  On syntax being considered is the following:

``` csharp
var slice1 = array.Slice(index: 1, length: 2); 
var slice2 = array[1..2];  

// The .. syntax creates a range value
Range r = 1..2;             // Index 1, Length 2
int[] slice3 = array[r];    // access T[] this[Range r]
```

### Runtime Change

The native representation of arrays will need to be updated in order to support this change.  Today arrays are allocated as a single object that *conceptually* has the following layout:

``` c 
struct array 
{
    MethodTable* arrayType;
    size_t length;
    Bounds* bounds;  // Multi-dimensional array bounds
    byte data[0];
}
```

The slice proposal would split arrays into two allocations: the actual array storage and the slice accessing the array:

``` c
struct array 
{
    MethodTable* arrayType;
    size_t length;
    Bounds* bounds;  // Multi-dimensional array bounds
    byte data[0];
}

struct slice
{
    int length;
    byte* data_start; // Points into storage->data
    array* storage;   
}
```

## Prototype Plan

The goal of the prototype is to evaluate the performance impact of these changes on a real world application.  In particular to measure the performance degredation which comes with having an extra indirection on array access. 

Previously the array data was inlined into the array allocation.  In slices it will be an extra indirection away.  The belief is this will have a minimal impact on performance.  In particular because in many cases we will be able to cache the lookup and eliminate the indirection. 

The prototype experiment will be done on Windows using the CoreCLR runtime.  The code changes will be done in the open on GitHub. All of the repos participating will use the branch `features/slices` for the changes (make it easy for customers to follow along).  

The changes will be tested on the C# compiler.  This code base makes heavy use of arrays and over time measurements have repeatedly shown array performance is a critical component of compiler throughput.  If there are serious regressions in this approach it should be noticable in their benchmarks.  

To create the prototype the following components will be updated

### Runtime

The runtime will be changed to operate in one of two modes: legacy arrays and slices.  Applications can opt into this mode by adding the following entry into `app.config`

``` xml
<configuration>
    <startup> 
        <slices />
    </startup>
</configuration>
```

Additionally the runtime will provide a new instance method on `T[]`:

``` csharp
T[] Slice(int start, int index)
```

When operating in legacy array mode this method will throw a `NotImplementedException`.  In slices mode it will allocate a new `slice` entry pointing to the same array storage with the specified bounds.  Basic argument validation will occur on the bounds to ensure they are valid.  

### RyuJIT

RyuJIT will be updated to support code generation for the new slice based array layout.  The change will be functional at first with not a lot of effort going into optimizing the change.  Instead we will let the C# compiler benchmarks drive our optimization efforts.  The goal is to hit the low hanging fruit revealed by the benchmarks and see where that takes us performance wise. 

### GC

The GC will be updated to support both of the runtime array allocation modes. 

## FAQ 

### Won't this break applications that depend on native memory layout? 

The slice feature requires us to change the native memory layout of array types.  This will break any application which has a dependency on the existing layout.  For example an application which expects to find the array storage at a fixed offset from the array object start will instead find potentially invalid memory. There are several Microsoft products which are potentially impacted by this, most notably Intellitrace and SOS.  It's reasonable to expect third party applications are also affected.  

The plan is to ship the runtime changes as part of a future in place update to deskop.  For example as a part of the 4.6.4 installation.  These deployments are shipped via Windows Updates and potentially affect billions of machines. These two items together mean this change must be disabled by default.  Applications must opt into this behavior out of necessity in order to preserve existing behavior.

Library authors who want to depend on slices will be able to do so by targeting applicable versions of Net Standard.  Essentially versions which only run on runtimes where slices are available.  Likely tooling updates will be needed to ensure this ends up with correct `app.config` entries.  That is very solvable. 

### Why support both modes in the prototype?

Given this is a prototype why go through the hassel of supporting multiple array modes in the runtime?  Why not instead just support slices and measure that?

Support for both modes is a necessity for shipping this feature.  One of the prototype goals is to evaluate the impact the change will have on our code base.  This likely add significant complexity to the JIT in particular hence the prototype is a good oppurtunity to try this out and evaluate the impact. 


