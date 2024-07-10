# Use an official PHP image with Apache
FROM php:7.4-apache

# Install necessary PHP extensions, Composer, and Java (required for Liquibase)
RUN apt-get update && apt-get install -y \
    libzip-dev \
    unzip \
    default-jre \
    wget \
    && docker-php-ext-install mysqli pdo pdo_mysql zip \
    && curl -sS https://getcomposer.org/installer | php -- --install-dir=/usr/local/bin --filename=composer

# Install Liquibase
RUN mkdir /liquibase && \
    wget -O /liquibase/liquibase.tar.gz https://github.com/liquibase/liquibase/releases/download/v4.28.0/liquibase-4.28.0.tar.gz && \
    tar -xzf /liquibase/liquibase.tar.gz -C /liquibase && \
    rm /liquibase/liquibase.tar.gz && \
    chmod +x /liquibase/liquibase

# Add Liquibase to PATH
ENV PATH="/liquibase:${PATH}"

# Download MySQL JDBC driver dynamically
RUN wget -O /liquibase/lib/mysql-connector-java.jar https://repo1.maven.org/maven2/mysql/mysql-connector-java/8.0.30/mysql-connector-java-8.0.30.jar

# Set working directory
WORKDIR /var/www/html

# Copy the application code to the container
COPY . /var/www/html/

# Install PHP dependencies
RUN composer install

# Set permissions (optional but recommended)
RUN chown -R www-data:www-data /var/www/html && \
    chmod -R 755 /var/www/html

# Enable Apache mod_rewrite (if needed for .htaccess)
RUN a2enmod rewrite

# Copy custom php.ini 
COPY php.ini /usr/local/etc/php/conf.d/

# Copy Liquibase changelog files
COPY ./liquibase /var/www/html/liquibase

# Create a script to run Liquibase and start Apache
COPY start.sh /var/www/html/start.sh
RUN chmod +x /var/www/html/start.sh

# Use the start script as the entry point
CMD ["/var/www/html/start.sh"]
