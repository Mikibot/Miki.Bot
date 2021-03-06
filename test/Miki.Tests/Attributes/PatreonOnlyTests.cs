﻿using System.Threading.Tasks;
using Miki.Attributes;
using Miki.Discord.Common;
using Miki.Framework.Commands;
using Miki.Services;
using Miki.Tests.Modules;
using Moq;
using Xunit;

namespace Miki.Tests.Attributes
{
    public class PatreonOnlyTests : BaseCommandTest
    {
        [Fact]
        public async Task IsPatronNullTest()
        {
            PatreonOnlyAttribute attribute = new PatreonOnlyAttribute();
            Assert.False(await attribute.CheckAsync(Mock));
        }

        [Fact]
        public async Task IsNotPatronTest()
        {
            PatreonOnlyAttribute attribute = new PatreonOnlyAttribute();
            var userMock = new Mock<IUserService>();
            userMock.Setup(x => x.UserIsDonatorAsync(It.IsAny<long>()))
                .Returns<long>(x => new ValueTask<bool>(x == 1L));

            var authorMock = new Mock<IDiscordUser>();
            authorMock.Setup(x => x.Id)
                .Returns(0L);

            var messageMock = new Mock<IDiscordMessage>();
            messageMock.SetupGet(x => x.Author)
                .Returns(authorMock.Object);

            Mock.SetContext(CorePipelineStage.MessageContextKey, messageMock.Object);

            Mock.SetService(typeof(IUserService), userMock.Object);
            Mock.SetService(typeof(IDiscordUser), authorMock.Object);

            Assert.False(await attribute.CheckAsync(Mock));
        }
    }
}
