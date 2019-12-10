﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using EFCore.TimeTraveler;
using EFCore.TimeTravelerTests.DataAccess;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore.TimeTravelerTests
{
    internal class Program
    {
        private static AutofacServiceProvider _serviceProvider;

        private static async Task Main(string[] args)
        {
            Configure();

            await ScaffoldDb();

            var appleId = Guid.NewGuid();

            await SetInitialFruitStatus(appleId);

            var unripeAppleTime = DateTime.UtcNow;

            await AsTimePasses();

            await UpdateFruitStatus(appleId, FruitStatus.Ripe);

            var ripeAppleTime = DateTime.UtcNow;

            await AsTimePasses();

            await UpdateFruitStatus(appleId, FruitStatus.Rotten, new[] {"Moe"});

            await GiveMoeFriendsAndWeapons(appleId);

            var rottenAppleTime = DateTime.UtcNow;

            await AsTimePasses();

            await UpdateFruitStatus(appleId, FruitStatus.Fuzzy, new[] {"Hairy", "Curly"});

            await GiveHairyAndCurlyFriendsAndWeapons(appleId);

            var fuzzyAppleTime = DateTime.UtcNow;

            //using var assertionScope = new AssertionScope();

            var currentApple = await GetApple(appleId);

            currentApple
                .Should()
                .BeEquivalentTo(
                    new
                    {
                        FruitStatus = FruitStatus.Fuzzy,
                        Worms = new object[]
                        {
                            new 
                            {
                                Name = "Hairy",
                                Weapons = new []
                                {
                                    new {Name = "Holy Hand Grenade"},
                                    new {Name = "Super Banana Bomb"}
                                },
                                FriendsNames = new []
                                {
                                    "Joan",
                                    "Starr",
                                    "Curly"
                                }

                            },
                            new {Name = "Curly"},
                            new 
                            {
                                Name = "Moe",
                                Weapons = new [] {new {Name = "Bazooka Pie" } },
                                FriendsNames = new []
                                {
                                    "John",
                                }

                            }
                        }
                    }, options => options.ExcludingMissingMembers());

            using (TemporalQuery.At(rottenAppleTime))
            {
                (await GetApple(appleId))
                    .Should()
                    .BeEquivalentTo(new
                        {
                            FruitStatus = FruitStatus.Rotten,
                            Worms = new object[]
                            {
                                new
                                {
                                    Name = "Moe",
                                    Weapons = new [] {new {Name = "Bazooka" } },
                                    FriendsNames = new []
                                    {
                                        "John",
                                        "Ringo"
                                    }

                                }
                            }
                        },
                        options => options.ExcludingMissingMembers());
            }

            
            await Task.WhenAll(new[]
            {
                AssertAtRipeAppleTime(ripeAppleTime, appleId),
                AssertAtUnripeAppleTime(unripeAppleTime, appleId),
                AssertAtRottenAppleTime(rottenAppleTime, appleId)
            });
        }

        private static async Task AssertAtRottenAppleTime(DateTime rottenAppleTime, Guid appleId)
        {
            using (TemporalQuery.At(rottenAppleTime))
            {
                using var localScope = _serviceProvider.CreateScope();
                var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();
                var rottenAppleWorms = await context.Apples
                    .Where(a => a.Id == appleId)
                    .Select(a => a.Worms)
                    .AsNoTracking().ToListAsync();

                rottenAppleWorms
                    .Should()
                    .BeEquivalentTo(new[] {new {Name = "Moe"}},
                        options => options.ExcludingMissingMembers());
            }
        }

        private static async Task AssertAtUnripeAppleTime(DateTime unripeAppleTime, Guid appleId)
        {
            using (TemporalQuery.At(unripeAppleTime))
            {
                (await GetApple(appleId))
                    .Should()
                    .BeEquivalentTo(new {FruitStatus = FruitStatus.Unripe, Worms = Array.Empty<Worm>()},
                        options => options.ExcludingMissingMembers());
            }
        }

        private static async Task AssertAtRipeAppleTime(DateTime ripeAppleTime, Guid appleId)
        {
            using (TemporalQuery.At(ripeAppleTime))
            {
                (await GetApple(appleId))
                    .Should()
                    .BeEquivalentTo(new {FruitStatus = FruitStatus.Ripe, Worms = Array.Empty<Worm>()},
                        options => options.ExcludingMissingMembers());
            }
        }

        private static async Task GiveMoeFriendsAndWeapons(Guid appleId)
        {
            using var localScope = _serviceProvider.CreateScope();

            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();

            var moe = await context.Apples
                .Where(a => a.Id == appleId)
                .SelectMany(a => a.Worms)
                .SingleAsync();

            moe.Weapons.Add(new WormWeapon {Name = "Bazooka"});

            moe.FriendshipsA.AddRange(new[]
            {
                new WormFriendship
                {
                    WormA = moe,
                    WormB = new Worm {Name = "John", Apple = new Apple {FruitStatus = FruitStatus.Ripe}}
                },
                new WormFriendship
                {
                    WormA = moe,
                    WormB = new Worm {Name = "Ringo", Apple = new Apple {FruitStatus = FruitStatus.Rotten}}
                }
            });

            await context.SaveChangesAsync();
        }

        private static async Task GiveHairyAndCurlyFriendsAndWeapons(Guid appleId)
        {
            using var localScope = _serviceProvider.CreateScope();

            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();

            var worms = await context.Apples
                .Where(a => a.Id == appleId)
                .SelectMany(a => a.Worms)
                .Include(worm => worm.Weapons)
                .Include(worm => worm.FriendshipsA)
                .ThenInclude(friendship => friendship.WormB)
                .ToListAsync();

            var curly = worms.Single(worm => worm.Name == "Curly");

            var hairy = worms.Single(worm => worm.Name == "Hairy");

            hairy.Weapons.Add(new WormWeapon {Name = "Super Banana Bomb"});
            hairy.Weapons.Add(new WormWeapon {Name = "Holy Hand Grenade"});

            var joan = new Worm {Name = "Joan", Apple = new Apple {FruitStatus = FruitStatus.Unripe}};
            joan.Weapons.Add(new WormWeapon {Name = "Dodgy Phone Battery"});
            joan.Weapons.Add(new WormWeapon {Name = "Angry Concrete Donkey"});

            hairy.FriendshipsA.AddRange(new[]
            {
                new WormFriendship {WormA = hairy, WormB = curly}, new WormFriendship {WormA = hairy, WormB = joan},
                new WormFriendship
                {
                    WormA = hairy,
                    WormB = new Worm {Name = "Starr", Apple = new Apple {FruitStatus = FruitStatus.Fuzzy}}
                }
            });

            var moe = worms.Single(worm => worm.Name == "Moe");
            moe.Weapons.First().Name = "Bazooka Pie";
            moe.FriendshipsA.RemoveAll(friendship => friendship.WormB.Name == "Ringo");

            await context.SaveChangesAsync();
        }

        private static async Task<Apple> GetApple(Guid appleId)
        {
            using var localScope = _serviceProvider.CreateScope();

            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();

            return await context.Apples
                .Include(apple => apple.Worms)
                .ThenInclude(worm => worm.Weapons)
                .Include(apple => apple.Worms)
                .ThenInclude(worm => worm.FriendshipsA)
                .ThenInclude(friendship => friendship.WormB)
                .ThenInclude(worm => worm.Weapons)
                .Include(apple => apple.Worms)
                .ThenInclude(worm => worm.FriendshipsB)
                .ThenInclude(friendship => friendship.WormA)
                .ThenInclude(worm => worm.Weapons)
                .Where(a => a.Id == appleId)
                .AsNoTracking()
                .SingleAsync();
        }


        private static async Task AsTimePasses()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        private static async Task ScaffoldDb()
        {
            using var localScope = _serviceProvider.CreateScope();

            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();

            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }

        private static void Configure()
        {
            var services = new ServiceCollection();
            var builder = new ContainerBuilder();

            services.AddDbContext<ApplicationDbContext>();

            builder.Populate(services);
            var appContainer = builder.Build();
            _serviceProvider = new AutofacServiceProvider(appContainer);
        }

        private static async Task UpdateFruitStatus(Guid appleId, FruitStatus fruitStatus, string[] worms = null)
        {
            using var localScope = _serviceProvider.CreateScope();
            worms ??= Array.Empty<string>();
            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();
            var myApple = await context.Apples.Where(a => a.Id == appleId).SingleAsync();

            myApple.FruitStatus = fruitStatus;
            myApple.Worms.AddRange(worms.Select(wormName => new Worm {Name = wormName}));

            await context.SaveChangesAsync();
        }

        private static async Task SetInitialFruitStatus(Guid appleId)
        {
            using var localScope = _serviceProvider.CreateScope();

            var context = localScope.ServiceProvider.GetService<ApplicationDbContext>();

            var myApple = new Apple {Id = appleId, FruitStatus = FruitStatus.Unripe};
            context.Apples.Add(myApple);

            await context.SaveChangesAsync();
        }
    }
}
