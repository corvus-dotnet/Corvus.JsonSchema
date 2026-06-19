# Minimal OpenLDAP test server built from Debian's maintained slapd (OpenLDAP 2.5.x in bookworm).
#
# Owning this image removes any dependency on a third-party registry namespace that could be
# purged the way docker.io/bitnami/openldap was (its versioned tags were deleted in Aug 2025 and
# only survive, frozen, under bitnamilegacy). Debian ships and security-patches OpenLDAP itself,
# so this is the durable, reproducible source for the LDAP conformance suite.
#
# The suffix (dc=example,dc=org) and the admin entry (cn=admin,dc=example,dc=org / adminpassword)
# are preseeded through debconf so the slapd post-install self-configures non-interactively at
# build time. The base suffix entry IS created by that bootstrap, so the test seed adds only the
# org-units and principals beneath it.
FROM debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

RUN set -eux; \
    { \
        echo "slapd slapd/no_configuration boolean false"; \
        echo "slapd slapd/domain string example.org"; \
        echo "slapd shared/organization string Example Inc."; \
        echo "slapd slapd/password1 password adminpassword"; \
        echo "slapd slapd/password2 password adminpassword"; \
        echo "slapd slapd/backend select MDB"; \
        echo "slapd slapd/purge_database boolean true"; \
        echo "slapd slapd/move_old_database boolean true"; \
    } | debconf-set-selections; \
    apt-get update; \
    apt-get install -y --no-install-recommends slapd ldap-utils; \
    rm -rf /var/lib/apt/lists/*

EXPOSE 389

# Run in the foreground (so the container stays up) at stats debug level. slapd opens its listen
# socket only once it is ready to serve, which is what the test's readiness probe waits on.
CMD ["/usr/sbin/slapd", "-h", "ldap:///", "-u", "openldap", "-g", "openldap", "-d", "256"]
