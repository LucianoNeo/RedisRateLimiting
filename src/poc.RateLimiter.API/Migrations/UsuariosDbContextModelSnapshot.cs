﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using poc.RateLimiter.Infraestrutura.Contexto;

#nullable disable

namespace poc.RateLimiter.API.Migrations
{
    [DbContext(typeof(UsuariosDbContext))]
    partial class UsuariosDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("poc.RateLimiter.API.Entidades.RateLimit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Endpoint")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("PermitLimit")
                        .HasColumnType("int");

                    b.Property<Guid>("UsuarioId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<TimeSpan>("Window")
                        .HasColumnType("time");

                    b.HasKey("Id");

                    b.HasIndex("UsuarioId");

                    b.ToTable("RateLimits");
                });

            modelBuilder.Entity("poc.RateLimiter.API.Entidades.Usuario", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Nome")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Usuarios");
                });

            modelBuilder.Entity("poc.RateLimiter.API.Entidades.RateLimit", b =>
                {
                    b.HasOne("poc.RateLimiter.API.Entidades.Usuario", "Usuario")
                        .WithMany("RateLimits")
                        .HasForeignKey("UsuarioId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Usuario");
                });

            modelBuilder.Entity("poc.RateLimiter.API.Entidades.Usuario", b =>
                {
                    b.Navigation("RateLimits");
                });
#pragma warning restore 612, 618
        }
    }
}
