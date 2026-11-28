using System.Runtime.InteropServices;
using KanbanApp.Services;

namespace KanbanApp.Tests;

// The message one copy of the app sends the open one to hand over a clicked task link.
public sealed class TaskLinkMessageTests
{
    [Theory]
    [InlineData("kanbantask://task/0123456789abcdef0123456789abcdef?file=fedcba9876543210fedcba9876543210")]
    [InlineData("")]
    [InlineData("é漢字")]
    public void WhatIsSent_IsReadBackTheSame(string link)
    {
        var data = TaskLinkMessage.Allocate(link, out var text);
        try
        {
            Assert.True(TaskLinkMessage.TryRead(data, out var read));
            Assert.Equal(link, read);
        }
        finally
        {
            Marshal.FreeHGlobal(text);
            Marshal.FreeHGlobal(data);
        }
    }

    [Fact]
    public void NoMessage_OrOneLongerThanALinkCanBe_IsNotOurs()
    {
        Assert.False(TaskLinkMessage.TryRead(IntPtr.Zero, out var none));
        Assert.Null(none);

        var data = TaskLinkMessage.Allocate(new string('x', 600), out var text); // 1,202 bytes with the null
        try
        {
            Assert.False(TaskLinkMessage.TryRead(data, out var tooLong));
            Assert.Null(tooLong);
        }
        finally
        {
            Marshal.FreeHGlobal(text);
            Marshal.FreeHGlobal(data);
        }
    }

    [Fact]
    public void AMessageFromAnotherProgram_IsNotOurs()
    {
        var data = TaskLinkMessage.Allocate("kanbantask://task/x", out var text);
        try
        {
            Marshal.WriteIntPtr(data, 0, 0x1234); // someone else's dwData
            Assert.False(TaskLinkMessage.TryRead(data, out _));
        }
        finally
        {
            Marshal.FreeHGlobal(text);
            Marshal.FreeHGlobal(data);
        }
    }
}
