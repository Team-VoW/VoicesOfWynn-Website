# Use an official PHP image with Apache
FROM php:8.1-apache

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

# List files in the current directory (for debug purposes)
RUN echo "Files in the current directory before copying:" && ls -la

# Copy the application code to the container
COPY . .

# Install PHP dependencies
RUN composer install

# Set permissions (optional but recommended)
RUN chown -R www-data:www-data /var/www/html && \
    chmod -R 755 /var/www/html

# Enable Apache mod_rewrite, SSL, and proxy modules
RUN a2enmod rewrite
RUN a2enmod ssl
RUN a2enmod proxy
RUN a2enmod proxy_http
RUN a2enmod headers

# Copy custom php.ini 
COPY php.ini /usr/local/etc/php/conf.d/

# Copy Liquibase changelog files
COPY ./liquibase /var/www/html/liquibase

# Copy SSL configuration file
COPY ssl.conf /etc/apache2/sites-available/ssl.conf

# Enable the SSL virtual host
#RUN a2ensite ssl.conf

# Create a script to run Liquibase and start Apache
COPY start.sh /start.sh
RUN chmod +x /start.sh

# Use the start script as the entry point
CMD ["/start.sh"]