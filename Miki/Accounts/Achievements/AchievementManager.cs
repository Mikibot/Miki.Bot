﻿using Miki.Framework;
using Miki.Common;
using Miki.Accounts.Achievements.Objects;
using Miki.Models;
using StatsdClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miki.Framework.Events;
using Discord;

namespace Miki.Accounts.Achievements
{
	public delegate Task<bool> CheckUserUpdateAchievement(IUser ub, IUser ua);

	public delegate Task<bool> CheckCommandAchievement(User u, CommandEvent e);

	public class AchievementManager
	{
		private static AchievementManager _instance = new AchievementManager(Bot.Instance);
		public static AchievementManager Instance => _instance;
		internal BaseService provider = null;

		private Bot bot;
		private Dictionary<string, AchievementDataContainer> containers = new Dictionary<string, AchievementDataContainer>();

		public event Func<AchievementPacket, Task> OnAchievementUnlocked;

		public event Func<CommandPacket, Task> OnCommandUsed;

		public event Func<LevelPacket, Task> OnLevelGained;

		public event Func<MessageEventPacket, Task> OnMessage;

		public event Func<TransactionPacket, Task> OnTransaction;

		public AchievementManager(Bot bot)
		{
			this.bot = bot;

			AccountManager.Instance.OnGlobalLevelUp += async (u, c, l) =>
			{
				if (await provider.IsEnabled(c.Id))
				{
					LevelPacket p = new LevelPacket()
					{
						discordUser = await (c as IGuildChannel).GetUserAsync(u.Id),
						discordChannel = c,
						level = l,
					};
					await OnLevelGained?.Invoke(p);
				}
			};

			AccountManager.Instance.OnTransactionMade += async (msg, u1, u2, amount) =>
			{
				if (await provider.IsEnabled(msg.Channel.Id))
				{
					TransactionPacket p = new TransactionPacket()
					{
						discordUser = msg.Author,
						discordChannel = msg.Channel,
						giver = u1,
						receiver = u2,
						amount = amount
					};

					await OnTransaction?.Invoke(p);
				}
			};

			EventSystem.Instance.AddCommandDoneEvent(x =>
			{
				x.Name = "--achievement-manager-command";
				x.processEvent = async (m, e, s, t) =>
				{
					CommandPacket p = new CommandPacket()
					{
						discordUser = m.Author,
						discordChannel = m.Channel,
						message = m,
						command = e,
						success = s
					};
					await OnCommandUsed?.Invoke(p);
				};
			});

			//bot.MessageReceived += async (msg) =>
			//{
			//	if (await provider.IsEnabled(msg.Channel.Id))
			//	{
			//		MessageEventPacket packet = new MessageEventPacket()
			//		{
			//			discordUser = msg.Author,
			//			discordChannel = msg.Channel,
			//			message = msg
			//		};

			//		await OnMessage(packet);
			//	}
			//};
		}

		internal void AddContainer(AchievementDataContainer container)
		{
			if (containers.ContainsKey(container.Name))
			{
				Log.WarningAt("AddContainer", "Cannot add duplicate containers");
				return;
			}

			containers.Add(container.Name, container);
		}

		public AchievementDataContainer GetContainerById(string id)
		{
			if (containers.ContainsKey(id))
			{
				return containers[id];
			}

			Log.Warning($"Could not load AchievementContainer {id}");
			return null;
		}

		public string PrintAchievements(List<Achievement> achievementNames)
		{
			string output = "";
			foreach(var a in achievementNames)
			{
				if (containers.TryGetValue(a.Name, out var value))
				{
					if(a.Rank < value.Achievements.Count)
					{
						output += value.Achievements[a.Rank].Icon + " "; 
					}
				}
			}
			return output;
		}

		public async Task CallAchievementUnlockEventAsync(BaseAchievement achievement, IUser user, IMessageChannel channel)
		{
			DogStatsd.Counter("achievements.gained", 1);

			if (achievement as AchievementAchievement != null)
				return;

			long id = user.Id.ToDbLong();

			using (var context = new MikiContext())
			{
				int achievementCount = await context.Achievements
					.Where(q => q.Id == id)
					.CountAsync();

				AchievementPacket p = new AchievementPacket()
				{
					discordUser = user,
					discordChannel = channel,
					achievement = achievement,
					count = achievementCount
				};

				await OnAchievementUnlocked?.Invoke(p);
			}
		}

		public async Task CallTransactionMadeEventAsync(IMessageChannel m, User receiver, User giver, int amount)
		{
			try
			{
				TransactionPacket p = new TransactionPacket();
				p.discordChannel = m;
				p.discordUser = Bot.Instance.Client.GetUser(receiver.Id.FromDbLong());

				if (giver != null)
				{
					p.giver = giver;
				}

				p.receiver = receiver;

				p.amount = amount;

				if (OnTransaction != null)
				{
					await OnTransaction?.Invoke(p);
				}
			}
			catch (Exception e)
			{
				Log.WarningAt("achievement check failed", e.ToString());
			}
		}
	}
}